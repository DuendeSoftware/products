// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Endpoints.Internal;
using Duende.Bff.Internal;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Duende.Bff.Tests.Endpoints;

public class BffAuthenticationServiceTests
{
    [Fact]
    public async Task challenge_for_bff_endpoint_should_not_call_inner_service()
    {
        var inner = Substitute.For<IAuthenticationService>();
        var sut = CreateSut(inner);
        var context = CreateContext(new BffApiAttribute());

        context.Response.Headers["Location"] = "https://identity-server/connect/authorize";
        context.Response.Headers["Set-Cookie"] = "temp=1";

        await sut.ChallengeAsync(context, BffAuthenticationSchemes.BffOpenIdConnect.ToString(), new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.Response.Headers.ContainsKey("Location").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Set-Cookie").ShouldBeFalse();
        _ = inner.DidNotReceive().ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task forbid_for_bff_endpoint_should_not_call_inner_service()
    {
        var inner = Substitute.For<IAuthenticationService>();
        var sut = CreateSut(inner);
        var context = CreateContext(new BffApiAttribute());

        context.Response.Headers["Location"] = "https://identity-server/connect/authorize";
        context.Response.Headers["Set-Cookie"] = "temp=1";

        await sut.ForbidAsync(context, BffAuthenticationSchemes.BffOpenIdConnect.ToString(), new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.Response.Headers.ContainsKey("Location").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Set-Cookie").ShouldBeFalse();
        _ = inner.DidNotReceive().ForbidAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task challenge_for_bff_endpoint_with_skipped_response_handling_should_call_inner_service()
    {
        var inner = Substitute.For<IAuthenticationService>();
        _ = inner.ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>())
            .Returns(call =>
            {
                var context = call.Arg<HttpContext>();
                context.Response.StatusCode = StatusCodes.Status302Found;
                context.Response.Headers["Location"] = "https://identity-server/connect/authorize";
                context.Response.Headers["Set-Cookie"] = "temp=1";
                return Task.CompletedTask;
            });

        var sut = CreateSut(inner);
        var context = CreateContext(new BffApiAttribute(), new BffApiSkipResponseHandlingAttribute());

        await sut.ChallengeAsync(context, "oidc", new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        context.Response.Headers["Location"].ToString().ShouldBe("https://identity-server/connect/authorize");
        context.Response.Headers["Set-Cookie"].ToString().ShouldBe("temp=1");
        _ = inner.Received(1).ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task challenge_for_bff_endpoint_with_non_bff_scheme_should_preserve_non_redirect_response()
    {
        var inner = Substitute.For<IAuthenticationService>();
        _ = inner.ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>())
            .Returns(call =>
            {
                var context = call.Arg<HttpContext>();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers["WWW-Authenticate"] = "Bearer api";
                return Task.CompletedTask;
            });

        var sut = CreateSut(inner);
        var context = CreateContext(new BffApiAttribute());

        await sut.ChallengeAsync(context, "Bearer", new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.Response.Headers["WWW-Authenticate"].ToString().ShouldBe("Bearer api");
        _ = inner.Received(1).ChallengeAsync(Arg.Any<HttpContext>(), "Bearer", Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task forbid_for_bff_endpoint_with_skipped_response_handling_should_call_inner_service()
    {
        var inner = Substitute.For<IAuthenticationService>();
        _ = inner.ForbidAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>())
            .Returns(call =>
            {
                var context = call.Arg<HttpContext>();
                context.Response.StatusCode = StatusCodes.Status302Found;
                context.Response.Headers["Location"] = "https://identity-server/connect/authorize";
                context.Response.Headers["Set-Cookie"] = "temp=1";
                return Task.CompletedTask;
            });

        var sut = CreateSut(inner);
        var context = CreateContext(new BffApiAttribute(), new BffApiSkipResponseHandlingAttribute());

        await sut.ForbidAsync(context, "oidc", new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        context.Response.Headers["Location"].ToString().ShouldBe("https://identity-server/connect/authorize");
        context.Response.Headers["Set-Cookie"].ToString().ShouldBe("temp=1");
        _ = inner.Received(1).ForbidAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task forbid_for_bff_endpoint_with_non_bff_scheme_should_preserve_non_redirect_response()
    {
        var inner = Substitute.For<IAuthenticationService>();
        _ = inner.ForbidAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>())
            .Returns(call =>
            {
                var context = call.Arg<HttpContext>();
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.Headers["WWW-Authenticate"] = "Bearer insufficient_scope";
                return Task.CompletedTask;
            });

        var sut = CreateSut(inner);
        var context = CreateContext(new BffApiAttribute());

        await sut.ForbidAsync(context, "Bearer", new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.Response.Headers["WWW-Authenticate"].ToString().ShouldBe("Bearer insufficient_scope");
        _ = inner.Received(1).ForbidAsync(Arg.Any<HttpContext>(), "Bearer", Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task challenge_with_null_scheme_for_bff_endpoint_should_not_call_inner_service()
    {
        var inner = Substitute.For<IAuthenticationService>();
        var sut = CreateSut(inner);
        var context = CreateContext(new BffApiAttribute());

        context.Response.Headers["Location"] = "https://identity-server/connect/authorize";
        context.Response.Headers["Set-Cookie"] = "temp=1";

        await sut.ChallengeAsync(context, scheme: null, new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.Response.Headers.ContainsKey("Location").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Set-Cookie").ShouldBeFalse();
        _ = inner.DidNotReceive().ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task forbid_with_null_scheme_for_bff_endpoint_should_not_call_inner_service()
    {
        var inner = Substitute.For<IAuthenticationService>();
        var sut = CreateSut(inner);
        var context = CreateContext(new BffApiAttribute());

        context.Response.Headers["Location"] = "https://identity-server/connect/authorize";
        context.Response.Headers["Set-Cookie"] = "temp=1";

        await sut.ForbidAsync(context, scheme: null, new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.Response.Headers.ContainsKey("Location").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Set-Cookie").ShouldBeFalse();
        _ = inner.DidNotReceive().ForbidAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task challenge_for_non_bff_endpoint_should_call_inner_service_and_preserve_response()
    {
        var inner = Substitute.For<IAuthenticationService>();
        _ = inner.ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>())
            .Returns(call =>
            {
                var context = call.Arg<HttpContext>();
                context.Response.StatusCode = StatusCodes.Status302Found;
                context.Response.Headers["Location"] = "https://identity-server/connect/authorize";
                context.Response.Headers["Set-Cookie"] = "temp=1";
                return Task.CompletedTask;
            });

        var sut = CreateSut(inner);
        var context = CreateContext();

        await sut.ChallengeAsync(context, "oidc", new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        context.Response.Headers["Location"].ToString().ShouldBe("https://identity-server/connect/authorize");
        context.Response.Headers["Set-Cookie"].ToString().ShouldBe("temp=1");
        _ = inner.Received(1).ChallengeAsync(context, "oidc", Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task forbid_for_non_bff_endpoint_should_call_inner_service_and_preserve_response()
    {
        var inner = Substitute.For<IAuthenticationService>();
        _ = inner.ForbidAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>())
            .Returns(call =>
            {
                var context = call.Arg<HttpContext>();
                context.Response.StatusCode = StatusCodes.Status302Found;
                context.Response.Headers["Location"] = "https://identity-server/connect/authorize";
                context.Response.Headers["Set-Cookie"] = "temp=1";
                return Task.CompletedTask;
            });

        var sut = CreateSut(inner);
        var context = CreateContext();

        await sut.ForbidAsync(context, "oidc", new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        context.Response.Headers["Location"].ToString().ShouldBe("https://identity-server/connect/authorize");
        context.Response.Headers["Set-Cookie"].ToString().ShouldBe("temp=1");
        _ = inner.Received(1).ForbidAsync(context, "oidc", Arg.Any<AuthenticationProperties?>());
    }

    [Theory]
    [InlineData("cookie")]
    [InlineData("oidc")]
    public async Task challenge_with_frontend_scheme_for_bff_endpoint_should_not_call_inner_service(string schemeType)
    {
        var inner = Substitute.For<IAuthenticationService>();
        var frontend = new BffFrontend(BffFrontendName.Parse("some-frontend"));
        var context = CreateContext(new BffApiAttribute());
        var sut = CreateSut(inner, context, frontend);

        var scheme = schemeType == "cookie"
            ? frontend.CookieSchemeName.ToString()
            : frontend.OidcSchemeName.ToString();

        context.Response.Headers["Location"] = "https://identity-server/connect/authorize";
        context.Response.Headers["Set-Cookie"] = "temp=1";

        await sut.ChallengeAsync(context, scheme, new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.Response.Headers.ContainsKey("Location").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Set-Cookie").ShouldBeFalse();
        _ = inner.DidNotReceive().ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>());
    }

    [Theory]
    [InlineData("cookie")]
    [InlineData("oidc")]
    public async Task forbid_with_frontend_scheme_for_bff_endpoint_should_not_call_inner_service(string schemeType)
    {
        var inner = Substitute.For<IAuthenticationService>();
        var frontend = new BffFrontend(BffFrontendName.Parse("some-frontend"));
        var context = CreateContext(new BffApiAttribute());
        var sut = CreateSut(inner, context, frontend);

        var scheme = schemeType == "cookie"
            ? frontend.CookieSchemeName.ToString()
            : frontend.OidcSchemeName.ToString();

        context.Response.Headers["Location"] = "https://identity-server/connect/authorize";
        context.Response.Headers["Set-Cookie"] = "temp=1";

        await sut.ForbidAsync(context, scheme, new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.Response.Headers.ContainsKey("Location").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Set-Cookie").ShouldBeFalse();
        _ = inner.DidNotReceive().ForbidAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task challenge_with_frontend_and_unrelated_scheme_for_bff_endpoint_should_call_inner_service()
    {
        var inner = Substitute.For<IAuthenticationService>();
        _ = inner.ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>())
            .Returns(call =>
            {
                var context = call.Arg<HttpContext>();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers["WWW-Authenticate"] = "Bearer api";
                return Task.CompletedTask;
            });

        var frontend = new BffFrontend(BffFrontendName.Parse("some-frontend"));
        var context = CreateContext(new BffApiAttribute());
        var sut = CreateSut(inner, context, frontend);

        await sut.ChallengeAsync(context, "Bearer", new AuthenticationProperties());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.Response.Headers["WWW-Authenticate"].ToString().ShouldBe("Bearer api");
        _ = inner.Received(1).ChallengeAsync(context, "Bearer", Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task authenticate_should_pass_scheme_through_unchanged()
    {
        var inner = Substitute.For<IAuthenticationService>();
        _ = inner.AuthenticateAsync(Arg.Any<HttpContext>(), "Bearer").Returns(AuthenticateResult.NoResult());

        var frontend = new BffFrontend(BffFrontendName.Parse("some-frontend"));
        var context = CreateContext();
        var sut = CreateSut(inner, context, frontend);

        _ = await sut.AuthenticateAsync(context, "Bearer");

        _ = await inner.Received(1).AuthenticateAsync(context, "Bearer");
    }

    [Fact]
    public async Task authenticate_with_null_scheme_should_pass_through_unchanged()
    {
        var inner = Substitute.For<IAuthenticationService>();
        var frontend = new BffFrontend(BffFrontendName.Parse("some-frontend"));
        var context = CreateContext();
        var sut = CreateSut(inner, context, frontend);

        _ = await sut.AuthenticateAsync(context, scheme: null);

        _ = await inner.Received(1).AuthenticateAsync(context, scheme: null);
    }

    [Fact]
    public async Task authenticate_with_no_frontend_selected_should_pass_through_unchanged()
    {
        var inner = Substitute.For<IAuthenticationService>();
        var context = CreateContext();
        var sut = CreateSut(inner, context);

        _ = await sut.AuthenticateAsync(context, "Bearer");

        _ = await inner.Received(1).AuthenticateAsync(context, "Bearer");
    }

    [Fact]
    public async Task sign_in_should_pass_scheme_through_unchanged()
    {
        var inner = Substitute.For<IAuthenticationService>();
        var frontend = new BffFrontend(BffFrontendName.Parse("some-frontend"));
        var context = CreateContext();
        var sut = CreateSut(inner, context, frontend);
        var principal = new ClaimsPrincipal();

        await sut.SignInAsync(context, "Bearer", principal, new AuthenticationProperties());

        await inner.Received(1).SignInAsync(context, "Bearer", principal, Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task sign_out_should_pass_scheme_through_unchanged()
    {
        var inner = Substitute.For<IAuthenticationService>();
        var frontend = new BffFrontend(BffFrontendName.Parse("some-frontend"));
        var context = CreateContext();
        var sut = CreateSut(inner, context, frontend);

        await sut.SignOutAsync(context, "Bearer", new AuthenticationProperties());

        await inner.Received(1).SignOutAsync(context, "Bearer", Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task sign_out_with_null_scheme_should_pass_through_unchanged()
    {
        var inner = Substitute.For<IAuthenticationService>();
        var frontend = new BffFrontend(BffFrontendName.Parse("some-frontend"));
        var context = CreateContext();
        var sut = CreateSut(inner, context, frontend);

        await sut.SignOutAsync(context, scheme: null, new AuthenticationProperties());

        await inner.Received(1).SignOutAsync(context, scheme: null, Arg.Any<AuthenticationProperties?>());
    }

    [Fact]
    public async Task sign_out_with_no_frontend_selected_should_pass_through_unchanged()
    {
        var inner = Substitute.For<IAuthenticationService>();
        var context = CreateContext();
        var sut = CreateSut(inner, context);

        await sut.SignOutAsync(context, "Bearer", new AuthenticationProperties());

        await inner.Received(1).SignOutAsync(context, "Bearer", Arg.Any<AuthenticationProperties?>());
    }

    private static BffAuthenticationService CreateSut(IAuthenticationService inner, HttpContext? context = null, BffFrontend? frontend = null)
    {
        var contextAccessor = new FakeHttpContextAccessor();
        if (context != null)
        {
            contextAccessor.HttpContext = context;
        }

        var frontendAccessor = new CurrentFrontendAccessor(contextAccessor);
        if (frontend != null)
        {
            frontendAccessor.Set(frontend);
        }

        return new BffAuthenticationService(new Decorator<IAuthenticationService>(inner), frontendAccessor,
            NullLogger<BffAuthenticationService>.Instance);
    }

    private static HttpContext CreateContext(params object[] metadata)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test-endpoint"));
        return context;
    }
}
