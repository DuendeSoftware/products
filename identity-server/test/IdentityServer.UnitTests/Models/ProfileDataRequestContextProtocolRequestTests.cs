// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace IdentityServer.UnitTests.Contexts;

public sealed class ProfileDataRequestContextProtocolRequestTests
{
    [Fact]
    public void ProtocolRequest_Accepts_ValidatedRequest()
    {
        var client = new Client { ClientId = "oidc-client" };
        var request = new ValidatedRequest { Client = client };

        var context = new ProfileDataRequestContext { ProtocolRequest = request };

        context.ProtocolRequest.ShouldBeSameAs(request);
    }

    [Fact]
    public void Pattern_Matching_On_ProtocolRequest_Identifies_ValidatedRequest()
    {
        var client = new Client { ClientId = "oidc-client" };
        IValidatedRequest request = new ValidatedRequest { Client = client };

        var context = new ProfileDataRequestContext { ProtocolRequest = request };

        var result = context.ProtocolRequest switch
        {
            ValidatedRequest oidc => oidc.Client.ClientId,
            _ => "unknown"
        };

        result.ShouldBe("oidc-client");
    }

    [Fact]
    public void ProtocolRequest_Application_Returns_Client_For_Oidc()
    {
        var client = new Client { ClientId = "oidc-client", ClientName = "OIDC" };
        var request = new ValidatedRequest { Client = client };

        var context = new ProfileDataRequestContext { ProtocolRequest = request };

        context.ProtocolRequest!.Application.ShouldBeSameAs(client);
        context.ProtocolRequest.Application!.Identifier.ShouldBe("oidc-client");
    }

    /// <summary>
    /// Fake IValidatedRequest for testing non-OIDC scenarios.
    /// Simulates what ValidatedAuthnRequest would look like in the SAML repo.
    /// </summary>
    private sealed class FakeValidatedRequest : IValidatedRequest
    {
        public IConnectedApplication? Application => new SamlServiceProvider
        {
            EntityId = "https://sp.example.com"
        };

        public ClaimsPrincipal? Subject { get; set; }

        public string? SessionId { get; set; }

        public string IssuerName { get; set; } = "https://fake-issuer.example.com";

        public IdentityServerOptions Options { get; set; } = new();
    }
}
