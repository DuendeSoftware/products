// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Services;

namespace UnitTests.Common;

internal sealed class StubSaml2IssuerNameService(string issuer = "https://idp.example.com") : ISaml2IssuerNameService
{
    public Task<string> GetCurrentAsync(Ct ct) => Task.FromResult(issuer);
}
