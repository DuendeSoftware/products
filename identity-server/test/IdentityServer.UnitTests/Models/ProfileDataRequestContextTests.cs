// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Contexts;

public sealed class ProfileDataRequestContextTests
{
    private static ClaimsPrincipal CreateSubject() =>
        new(new ClaimsIdentity(new[] { new Claim("sub", "123") }));

    [Fact]
    public void Client_Constructor_Populates_Application()
    {
        var client = new Client { ClientId = "oidc-client", ClientName = "OIDC Client" };
        var context = new ProfileDataRequestContext(CreateSubject(), client, "caller", Array.Empty<string>());

        context.Application.ShouldNotBeNull();
        context.Application.Identifier.ShouldBe("oidc-client");
        context.Application.DisplayName.ShouldBe("OIDC Client");
        context.Application.ShouldBeSameAs(client);
    }

    [Fact]
    public void Application_Constructor_With_Client_Sets_Application()
    {
        var client = new Client { ClientId = "oidc-client" };
        var context = new ProfileDataRequestContext(CreateSubject(), (IConnectedApplication)client, "caller", Array.Empty<string>());

        context.Application.ShouldBeSameAs(client);
    }

    [Fact]
    public void Application_Constructor_With_SamlServiceProvider_Sets_Application_Only()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com", DisplayName = "Test SP" };
        var context = new ProfileDataRequestContext(CreateSubject(), sp, "caller", Array.Empty<string>());

        context.Application.ShouldBeSameAs(sp);
        context.Application.Identifier.ShouldBe("https://sp.example.com");
        context.Application.DisplayName.ShouldBe("Test SP");
    }

    [Fact]
    public void Application_Setter_With_Client_Updates_Application()
    {
        var client = new Client { ClientId = "test-client" };
        var context = new ProfileDataRequestContext();

        context.Application = client;

        context.Application.ShouldBeSameAs(client);
    }

    [Fact]
    public void Application_Setter_With_SamlServiceProvider_Sets_Application_Only()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com" };
        var context = new ProfileDataRequestContext();

        context.Application = sp;

        context.Application.ShouldBeSameAs(sp);
    }

    [Fact]
    public void Pattern_Matching_Works_On_SamlServiceProvider_Application()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com" };
        var context = new ProfileDataRequestContext(CreateSubject(), sp, "caller", Array.Empty<string>());

        var result = context.Application switch
        {
            SamlServiceProvider saml => saml.EntityId,
            Client client => client.ClientId,
            _ => "unknown"
        };

        result.ShouldBe("https://sp.example.com");
    }

    [Fact]
    public void Pattern_Matching_Works_On_Client_Application()
    {
        var client = new Client { ClientId = "oidc-client" };
        var context = new ProfileDataRequestContext(CreateSubject(), client, "caller", Array.Empty<string>());

        var result = context.Application switch
        {
            SamlServiceProvider saml => saml.EntityId,
            Client c => c.ClientId,
            _ => "unknown"
        };

        result.ShouldBe("oidc-client");
    }

    [Fact]
    public void Constructor_Throws_On_Null_Subject()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com" };
        Should.Throw<ArgumentNullException>(() =>
            new ProfileDataRequestContext(null!, sp, "caller", Array.Empty<string>()));
    }

    [Fact]
    public void Constructor_Throws_On_Null_Application() =>
        Should.Throw<ArgumentNullException>(() =>
            new ProfileDataRequestContext(CreateSubject(), ((IConnectedApplication)null!)!, "caller", Array.Empty<string>()));
}
