// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace UnitTests.Configuration.DependencyInjection;

public class IdentityServerCookieBuilderTests
{
    private static HttpContext CreateHttpContext(string basePath)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IServerUrls>(new MockServerUrls { BasePath = basePath });

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
    }

    public static TheoryData<string, string> PathResolutionCases => new()
    {
        { null, "/" },
        { "", "/" },
        { "/", "/" },
        { "/hosting-pathbase", "/hosting-pathbase" },
        { "/space", "/space" },
        { "/hosting-pathbase/space", "/hosting-pathbase/space" },
    };

    [Theory]
    [MemberData(nameof(PathResolutionCases))]
    public void resolved_path_matches_expected_for_root_hosting_space_and_combined_base_paths(string basePath,
        string expectedPath)
    {
        var context = CreateHttpContext(basePath);
        var innerBuilder = new CookieBuilder { Name = "test-cookie" };
        var sut = IdentityServerCookieBuilder.Wrap(innerBuilder);

        var options = sut.Build(context, DateTimeOffset.UtcNow);

        options.Path.ShouldBe(expectedPath);
    }

    [Fact]
    public async Task reusing_one_options_snapshot_across_concurrent_contexts_produces_independent_paths_without_mutating_original()
    {
        var innerBuilder = new CookieBuilder
        {
            Name = "shared-cookie",
            Path = "/should-never-be-observed"
        };
        var sut = IdentityServerCookieBuilder.Wrap(innerBuilder);

        var basePaths = Enumerable.Range(0, 25).Select(i => $"/space-{i}").ToArray();

        var results = await Task.WhenAll(basePaths.Select(async basePath =>
        {
            await Task.Yield();
            var context = CreateHttpContext(basePath);
            var options = sut.Build(context, DateTimeOffset.UtcNow);
            return (basePath, options.Path);
        }));

        foreach (var (basePath, resolvedPath) in results)
        {
            resolvedPath.ShouldBe(basePath);
        }

        // The original builder and its own Path must never be mutated by concurrent Build calls.
        innerBuilder.Path.ShouldBe("/should-never-be-observed");
    }

    [Fact]
    public void custom_builder_output_is_retained_except_for_path()
    {
        var expiresFrom = DateTimeOffset.UtcNow;
        var innerBuilder = new CookieBuilder
        {
            Name = "custom-cookie",
            Path = "/original-path",
            Domain = "example.com",
            SecurePolicy = CookieSecurePolicy.Always,
            SameSite = SameSiteMode.Strict,
            HttpOnly = false,
            IsEssential = true,
            MaxAge = TimeSpan.FromMinutes(5),
            Expiration = TimeSpan.FromMinutes(10)
        };
        innerBuilder.Extensions.Add("custom-extension=1");

        var sut = IdentityServerCookieBuilder.Wrap(innerBuilder);
        var context = CreateHttpContext("/resolved-path");

        var expected = innerBuilder.Build(context, expiresFrom);
        var actual = sut.Build(context, expiresFrom);

        actual.Domain.ShouldBe(expected.Domain);
        actual.Secure.ShouldBe(expected.Secure);
        actual.SameSite.ShouldBe(expected.SameSite);
        actual.HttpOnly.ShouldBe(expected.HttpOnly);
        actual.IsEssential.ShouldBe(expected.IsEssential);
        actual.MaxAge.ShouldBe(expected.MaxAge);
        actual.Expires.ShouldBe(expected.Expires);
        actual.Extensions.ShouldBe(expected.Extensions);

        actual.Path.ShouldBe("/resolved-path");
        actual.Path.ShouldNotBe(expected.Path);
    }

    private sealed class CustomCookieBuilder : CookieBuilder
    {
        public override CookieOptions Build(HttpContext context, DateTimeOffset expiresFrom)
        {
            var options = base.Build(context, expiresFrom);
            options.Extensions.Add("from-custom-subclass=1");
            return options;
        }
    }

    [Fact]
    public void wrapping_retains_original_builder_instance_for_custom_subclasses()
    {
        var custom = new CustomCookieBuilder { Name = "custom-subclass-cookie" };
        var sut = IdentityServerCookieBuilder.Wrap(custom);
        var context = CreateHttpContext("/resolved");

        var options = sut.Build(context, DateTimeOffset.UtcNow);

        options.Extensions.ShouldContain("from-custom-subclass=1");
        options.Path.ShouldBe("/resolved");
    }

    [Fact]
    public void wrapping_an_already_wrapped_builder_does_not_nest_or_alter_behavior()
    {
        var innerBuilder = new CookieBuilder { Name = "wrap-me" };

        var wrappedOnce = IdentityServerCookieBuilder.Wrap(innerBuilder);
        var wrappedTwice = IdentityServerCookieBuilder.Wrap(wrappedOnce);

        wrappedTwice.ShouldBeSameAs(wrappedOnce);

        var context = CreateHttpContext("/double-wrap");
        var options = wrappedTwice.Build(context, DateTimeOffset.UtcNow);

        options.Path.ShouldBe("/double-wrap");
    }

    [Fact]
    public void host_prefixed_cookie_at_non_root_resolved_path_throws_actionable_error()
    {
        var innerBuilder = new CookieBuilder { Name = "__Host-idsrv", SecurePolicy = CookieSecurePolicy.Always };
        var sut = IdentityServerCookieBuilder.Wrap(innerBuilder);
        var context = CreateHttpContext("/hosting-pathbase");

        var exception = Should.Throw<InvalidOperationException>(() => sut.Build(context, DateTimeOffset.UtcNow));

        exception.Message.ShouldContain("__Host-idsrv");
        exception.Message.ShouldContain("/hosting-pathbase");
        exception.Message.ShouldContain("__Secure-");
    }

    [Fact]
    public void host_prefixed_cookie_at_root_resolved_path_is_not_rejected_by_path_check()
    {
        var innerBuilder = new CookieBuilder
        {
            Name = "__Host-idsrv",
            SecurePolicy = CookieSecurePolicy.Always
        };
        var sut = IdentityServerCookieBuilder.Wrap(innerBuilder);
        var context = CreateHttpContext("/");

        var options = sut.Build(context, DateTimeOffset.UtcNow);

        options.Path.ShouldBe("/");
        options.Secure.ShouldBeTrue();
        options.Domain.ShouldBeNull();
    }

    [Fact]
    public void secure_prefixed_cookie_works_at_non_root_path_under_https()
    {
        var innerBuilder = new CookieBuilder
        {
            Name = "__Secure-idsrv",
            SecurePolicy = CookieSecurePolicy.Always,
            Domain = "example.com"
        };
        var sut = IdentityServerCookieBuilder.Wrap(innerBuilder);
        var context = CreateHttpContext("/space");
        context.Request.Scheme = "https";

        var options = sut.Build(context, DateTimeOffset.UtcNow);

        options.Path.ShouldBe("/space");
        options.Secure.ShouldBeTrue();
        options.Domain.ShouldBe("example.com");
    }

    [Fact]
    public void secure_prefixed_cookie_still_fails_under_configurations_the_framework_considers_insecure()
    {
        var innerBuilder = new CookieBuilder
        {
            Name = "__Secure-idsrv",
            SecurePolicy = CookieSecurePolicy.None
        };
        var sut = IdentityServerCookieBuilder.Wrap(innerBuilder);
        var context = CreateHttpContext("/space");
        context.Request.Scheme = "http";

        var options = sut.Build(context, DateTimeOffset.UtcNow);

        // The wrapper does not relax framework enforcement; SecurePolicy.None over http yields Secure=false,
        // which is not a valid __Secure- cookie. Enforcement of this remains the framework's/consumer's
        // responsibility (e.g. requiring HTTPS in production), not this path-prefix check.
        options.Secure.ShouldBeFalse();
    }

    [Fact]
    public void extension_added_to_wrapper_after_wrapping_is_included_in_built_options()
    {
        var innerBuilder = new CookieBuilder { Name = "wrapper-extension-cookie" };
        var sut = IdentityServerCookieBuilder.Wrap(innerBuilder);
        sut.Extensions.Add("partitioned");

        var context = CreateHttpContext("/space");
        var options = sut.Build(context, DateTimeOffset.UtcNow);

        options.Extensions.ShouldContain("partitioned");
    }

    [Fact]
    public void configured_domain_is_preserved_for_ordinary_cookie_name()
    {
        var innerBuilder = new CookieBuilder
        {
            Name = "idsrv",
            Domain = "example.com"
        };
        var sut = IdentityServerCookieBuilder.Wrap(innerBuilder);
        var context = CreateHttpContext("/space");

        var options = sut.Build(context, DateTimeOffset.UtcNow);

        options.Domain.ShouldBe("example.com");
    }
}
