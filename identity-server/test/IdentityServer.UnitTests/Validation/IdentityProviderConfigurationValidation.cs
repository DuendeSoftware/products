// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace UnitTests.Validation;

public class IdentityProviderConfigurationValidation
{
    private const string Category = "IdentityProvider Configuration Validation Tests";
    private IIdentityProviderConfigurationValidator _validator;
    private IdentityServerOptions _options;

    public IdentityProviderConfigurationValidation()
    {
        _options = new IdentityServerOptions();
        _options.DynamicProviders.AddProviderType<OpenIdConnectHandler, OpenIdConnectOptions, OidcProvider>("oidc");

        _validator = new DefaultIdentityProviderConfigurationValidator(_options);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task correctly_populated_idp_should_succeed()
    {
        var idp = new OidcProvider
        {
            Scheme = "scheme",
            ClientId = "client",
            ClientSecret = "secret",
            Authority = "authority",
            ResponseType = "code",
            Scope = "openid scope",
        };

        var ctx = new IdentityProviderConfigurationValidationContext(idp);
        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_type_registration_should_fail()
    {
        var idp = new IdentityProvider("type")
        {
            Scheme = "scheme",
        };

        var ctx = new IdentityProviderConfigurationValidationContext(idp);
        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldContain("registered");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task custom_type_registration_should_succeed()
    {
        _options.DynamicProviders.AddProviderType<OpenIdConnectHandler, OpenIdConnectOptions, OidcProvider>("type");

        var idp = new IdentityProvider("type")
        {
            Scheme = "scheme"
        };

        var ctx = new IdentityProviderConfigurationValidationContext(idp);
        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_scheme_should_fail()
    {
        var idp = new OidcProvider
        {
            ClientId = "client",
            ClientSecret = "secret",
            Authority = "authority",
            ResponseType = "code",
            Scope = "openid scope",
        };
        idp.Scheme = "";

        var ctx = new IdentityProviderConfigurationValidationContext(idp);
        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ToLowerInvariant().ShouldContain("scheme");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_clientid_should_fail()
    {
        var idp = new OidcProvider
        {
            Scheme = "scheme",
            ClientId = "",
            ClientSecret = "secret",
            Authority = "authority",
            ResponseType = "code",
            Scope = "openid scope",
        };

        var ctx = new IdentityProviderConfigurationValidationContext(idp);
        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ToLowerInvariant().ShouldContain("clientid");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_secret_should_be_allowed()
    {
        // we allow no secret because they might pull it from somewhere else
        var idp = new OidcProvider
        {
            Scheme = "scheme",
            ClientId = "client",
            ClientSecret = "",
            Authority = "authority",
            ResponseType = "code",
            Scope = "openid scope",
        };

        var ctx = new IdentityProviderConfigurationValidationContext(idp);
        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_authority_should_fail()
    {
        var idp = new OidcProvider
        {
            Scheme = "scheme",
            ClientId = "client",
            ClientSecret = "secret",
            Authority = "",
            ResponseType = "code",
            Scope = "openid scope",
        };

        var ctx = new IdentityProviderConfigurationValidationContext(idp);
        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ToLowerInvariant().ShouldContain("authority");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_responsetype_should_fail()
    {
        var idp = new OidcProvider
        {
            Scheme = "scheme",
            ClientId = "client",
            ClientSecret = "secret",
            Authority = "authority",
            ResponseType = "",
            Scope = "openid scope",
        };

        var ctx = new IdentityProviderConfigurationValidationContext(idp);
        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ToLowerInvariant().ShouldContain("responsetype");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_scope_should_fail()
    {
        var idp = new OidcProvider
        {
            Scheme = "scheme",
            ClientId = "client",
            ClientSecret = "secret",
            Authority = "authority",
            ResponseType = "code",
            Scope = "",
        };

        var ctx = new IdentityProviderConfigurationValidationContext(idp);
        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ToLowerInvariant().ShouldContain("scope");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_implicit_flow_missing_clientid_should_succeed()
    {
        var idp = new OidcProvider
        {
            Scheme = "scheme",
            ClientId = "client",
            ClientSecret = "",
            Authority = "authority",
            ResponseType = "id_token",
            Scope = "openid scope",
        };

        var ctx = new IdentityProviderConfigurationValidationContext(idp);
        await _validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
    }
}
