// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;

namespace IdentityServer.UnitTests.Contexts;

public sealed class AuthenticationContextTests
{
    [Fact]
    public void AuthorizationRequest_Application_Maps_To_Client()
    {
        var client = new Client { ClientId = "oidc-client", ClientName = "OIDC Client" };
        var request = new AuthorizationRequest { Client = client };

        var context = (IAuthenticationContext)request;

        context.Application.ShouldBeSameAs(client);
        context.Application.Identifier.ShouldBe("oidc-client");
        context.Application.DisplayName.ShouldBe("OIDC Client");
    }

    [Fact]
    public void AuthorizationRequest_IdP_Passes_Through()
    {
        var request = new AuthorizationRequest
        {
            Client = new Client { ClientId = "c" },
            IdP = "google"
        };

        var context = (IAuthenticationContext)request;

        context.IdP.ShouldBe("google");
    }

    [Fact]
    public void AuthorizationRequest_LoginHint_Passes_Through()
    {
        var request = new AuthorizationRequest
        {
            Client = new Client { ClientId = "c" },
            LoginHint = "alice@example.com"
        };

        var context = (IAuthenticationContext)request;

        context.LoginHint.ShouldBe("alice@example.com");
    }

    [Fact]
    public void AuthorizationRequest_Tenant_Passes_Through()
    {
        var request = new AuthorizationRequest
        {
            Client = new Client { ClientId = "c" },
            Tenant = "tenant-1"
        };

        var context = (IAuthenticationContext)request;

        context.Tenant.ShouldBe("tenant-1");
    }

    [Fact]
    public void AuthorizationRequest_Null_IdP_LoginHint_Tenant_Returns_Null()
    {
        var request = new AuthorizationRequest
        {
            Client = new Client { ClientId = "c" }
        };

        var context = (IAuthenticationContext)request;

        context.IdP.ShouldBeNull();
        context.LoginHint.ShouldBeNull();
        context.Tenant.ShouldBeNull();
    }

    [Fact]
    public void AuthorizationRequest_Does_Not_Expose_Application_On_Public_Api()
    {
        // Application is explicit interface only — preserves the Client-centric public API.
        var type = typeof(AuthorizationRequest);
        type.GetProperty("Application").ShouldBeNull();
    }

    [Fact]
    public void SamlAuthenticationContext_Application_Maps_To_ServiceProvider()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com", DisplayName = "Test SP" };
        var request = new SamlAuthenticationContext { ServiceProvider = sp };

        var context = (IAuthenticationContext)request;

        context.Application.ShouldBeSameAs(sp);
        context.Application.Identifier.ShouldBe("https://sp.example.com");
        context.Application.DisplayName.ShouldBe("Test SP");
    }

    [Fact]
    public void SamlAuthenticationContext_IdP_Returns_Null()
    {
        var request = new SamlAuthenticationContext
        {
            ServiceProvider = new SamlServiceProvider { EntityId = "https://sp.example.com" }
        };

        var context = (IAuthenticationContext)request;

        context.IdP.ShouldBeNull();
    }

    [Fact]
    public void SamlAuthenticationContext_LoginHint_Returns_Null()
    {
        var request = new SamlAuthenticationContext
        {
            ServiceProvider = new SamlServiceProvider { EntityId = "https://sp.example.com" }
        };

        var context = (IAuthenticationContext)request;

        context.LoginHint.ShouldBeNull();
    }

    [Fact]
    public void SamlAuthenticationContext_Tenant_Returns_Null()
    {
        var request = new SamlAuthenticationContext
        {
            ServiceProvider = new SamlServiceProvider { EntityId = "https://sp.example.com" }
        };

        var context = (IAuthenticationContext)request;

        context.Tenant.ShouldBeNull();
    }

    [Fact]
    public void SamlAuthenticationContext_Uses_Explicit_Interface_For_Application()
    {
        // Application is explicit — the public API exposes ServiceProvider instead.
        var type = typeof(SamlAuthenticationContext);
        type.GetProperty("Application").ShouldBeNull();
        type.GetProperty("ServiceProvider").ShouldNotBeNull();
    }

    [Fact]
    public void Pattern_Matching_Identifies_AuthorizationRequest()
    {
        var client = new Client { ClientId = "oidc-client" };
        IAuthenticationContext context = new AuthorizationRequest { Client = client };

        var result = context switch
        {
            AuthorizationRequest oidc => oidc.Client.ClientId,
            SamlAuthenticationContext saml => saml.ServiceProvider.EntityId,
            _ => "unknown"
        };

        result.ShouldBe("oidc-client");
    }

    [Fact]
    public void Pattern_Matching_Identifies_SamlAuthenticationContext()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com" };
        IAuthenticationContext context = new SamlAuthenticationContext { ServiceProvider = sp };

        var result = context switch
        {
            AuthorizationRequest oidc => oidc.Client.ClientId,
            SamlAuthenticationContext saml => saml.ServiceProvider.EntityId,
            _ => "unknown"
        };

        result.ShouldBe("https://sp.example.com");
    }

    [Fact]
    public void Pattern_Matching_On_Application_Distinguishes_Client_And_ServiceProvider()
    {
        var client = new Client { ClientId = "oidc-client" };
        IAuthenticationContext context = new AuthorizationRequest { Client = client };

        var identifier = context.Application switch
        {
            Client c => c.ClientId,
            SamlServiceProvider sp => sp.EntityId,
            _ => "unknown"
        };

        identifier.ShouldBe("oidc-client");
    }
}
