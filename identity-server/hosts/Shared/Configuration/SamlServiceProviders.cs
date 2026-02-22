// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Hosts.Shared.Configuration;

public static class SamlServiceProviders
{
    public static IEnumerable<SamlServiceProvider> Get() =>
    [
        new SamlServiceProvider
        {
            EntityId = "https://localhost:44350/Saml2",
            DisplayName = "MvcSaml Sample Client",
            Enabled = true,
            // ACS URL follows the Sustainsys.Saml2 convention: <base>/Saml2/Acs
            AssertionConsumerServiceUrls = [new Uri("https://localhost:44350/Saml2/Acs")],
            // Sign the assertion (not the response envelope) — the Sustainsys default expectation
            SigningBehavior = SamlSigningBehavior.SignAssertion,
            // No RequireSignedAuthnRequests — keeps the sample self-contained without distributing an SP cert
            // No EncryptAssertions — plain HTTP is fine for local development
        }
    ];
}
