// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.Profiles;

namespace IdentityServer.UnitTests.Configuration.Profiles;

public class PropertyExpressionHelperTests
{
    private const string Category = "PropertyExpressionHelper";

    [Fact]
    [Trait("Category", Category)]
    public void should_extract_top_level_property_path()
    {
        var options = new IdentityServerOptions();
        var (path, getter, setter) = PropertyExpressionParser.Parse(opt => opt.JwtValidationClockSkew, options);

        path.ShouldBe("JwtValidationClockSkew");
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_extract_nested_property_path()
    {
        var options = new IdentityServerOptions();
        var (path, getter, setter) = PropertyExpressionParser.Parse(opt => opt.PushedAuthorization.Required, options);

        path.ShouldBe("PushedAuthorization.Required");
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_extract_deeply_nested_property_path()
    {
        var options = new IdentityServerOptions();
        var (path, getter, setter) = PropertyExpressionParser.Parse(opt => opt.PushedAuthorization.Lifetime, options);

        path.ShouldBe("PushedAuthorization.Lifetime");
    }

    [Fact]
    [Trait("Category", Category)]
    public void getter_should_read_top_level_property()
    {
        var options = new IdentityServerOptions
        {
            JwtValidationClockSkew = TimeSpan.FromMinutes(10)
        };
        var (path, getter, setter) = PropertyExpressionParser.Parse(opt => opt.JwtValidationClockSkew, options);

        getter().ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    [Trait("Category", Category)]
    public void getter_should_read_nested_property()
    {
        var options = new IdentityServerOptions();
        options.PushedAuthorization.Required = true;

        var (path, getter, setter) = PropertyExpressionParser.Parse(opt => opt.PushedAuthorization.Required, options);

        getter().ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void setter_should_write_top_level_property()
    {
        var options = new IdentityServerOptions
        {
            JwtValidationClockSkew = TimeSpan.FromMinutes(5)
        };
        var (path, getter, setter) = PropertyExpressionParser.Parse(opt => opt.JwtValidationClockSkew, options);

        setter(TimeSpan.FromMinutes(10));

        options.JwtValidationClockSkew.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    [Trait("Category", Category)]
    public void setter_should_write_nested_property()
    {
        var options = new IdentityServerOptions();
        options.PushedAuthorization.Required = false;

        var (path, getter, setter) = PropertyExpressionParser.Parse(opt => opt.PushedAuthorization.Required, options);

        setter(true);

        options.PushedAuthorization.Required.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void getter_reflects_changes_made_directly()
    {
        var options = new IdentityServerOptions();
        var (path, getter, setter) = PropertyExpressionParser.Parse(opt => opt.PushedAuthorization.Required, options);

        options.PushedAuthorization.Required = true;

        getter().ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_work_with_nullable_string()
    {
        var options = new IdentityServerOptions
        {
            IssuerUri = "https://example.com"
        };
        var (path, getter, setter) = PropertyExpressionParser.Parse(opt => opt.IssuerUri, options);

        path.ShouldBe("IssuerUri");
        getter().ShouldBe("https://example.com");

        setter("https://newissuer.com");
        options.IssuerUri.ShouldBe("https://newissuer.com");
    }
}
