// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.ApplicationTests;

public class ApplicationInterfaceTests
{
    [Fact]
    public void Client_Implements_IConnectedApplication_With_Correct_Mappings()
    {
        var client = new Client
        {
            ClientId = "test-client-id",
            ClientName = "Test Client",
            Description = "A test client",
            Enabled = true,
            ProtocolType = "oidc",
            RequireConsent = true
        };

        var app = (IConnectedApplication)client;

        app.Identifier.ShouldBe("test-client-id");
        app.DisplayName.ShouldBe("Test Client");
        app.Description.ShouldBe("A test client");
        app.Enabled.ShouldBeTrue();
        app.ProtocolType.ShouldBe("oidc");
        app.RequireConsent.ShouldBeTrue();
    }

    [Fact]
    public void Client_IConnectedApplication_Identifier_Maps_ClientId()
    {
        var client = new Client { ClientId = "my-client" };
        var app = (IConnectedApplication)client;
        app.Identifier.ShouldBe("my-client");
    }

    [Fact]
    public void Client_IConnectedApplication_ProtocolType_Reflects_Client_ProtocolType()
    {
        var client = new Client { ProtocolType = "wsfed" };
        var app = (IConnectedApplication)client;
        app.ProtocolType.ShouldBe("wsfed");
    }

    [Fact]
    public void SamlServiceProvider_Implements_IConnectedApplication_With_Correct_Mappings()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test SP",
            Description = "A test SP",
            Enabled = true
        };

        var app = (IConnectedApplication)sp;

        app.Identifier.ShouldBe("https://sp.example.com");
        app.DisplayName.ShouldBe("Test SP");
        app.Description.ShouldBe("A test SP");
        app.Enabled.ShouldBeTrue();
        app.ProtocolType.ShouldBe("saml2p");
        app.RequireConsent.ShouldBeFalse();
    }

    [Fact]
    public void SamlServiceProvider_IConnectedApplication_Identifier_Maps_EntityId()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com" };
        var app = (IConnectedApplication)sp;
        app.Identifier.ShouldBe("https://sp.example.com");
    }

    [Fact]
    public void SamlServiceProvider_IConnectedApplication_ProtocolType_Is_Saml2p()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com" };
        var app = (IConnectedApplication)sp;
        app.ProtocolType.ShouldBe(IdentityServerConstants.ProtocolTypes.Saml2p);
    }

    [Fact]
    public void Client_Does_Not_Expose_IConnectedApplication_Members_On_Public_Api()
    {
        // Verify explicit interface implementation — IConnectedApplication members should not
        // appear as public properties directly on Client.
        var clientType = typeof(Client);
        clientType.GetProperty("Identifier").ShouldBeNull();
        clientType.GetProperty("ProtocolType").ShouldNotBeNull(); // ProtocolType IS a real Client property
    }

    [Fact]
    public void SamlServiceProvider_Does_Not_Expose_IConnectedApplication_Members_On_Public_Api()
    {
        // Verify explicit interface implementation — IConnectedApplication members should not
        // appear as public properties directly on SamlServiceProvider.
        var spType = typeof(SamlServiceProvider);
        spType.GetProperty("Identifier").ShouldBeNull();
        spType.GetProperty("ProtocolType").ShouldBeNull();
    }
}
