// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Services;

internal class Saml2IssuerNameService(
    IIssuerNameService issuerNameService,
    IOptions<IdentityServerOptions> identityServerOptions)
    : ISaml2IssuerNameService
{
    public async Task<string> GetCurrentAsync(Ct ct)
    {
        var saml = identityServerOptions.Value.Saml;
        return saml.EntityId ?? (await issuerNameService.GetCurrentAsync(ct)) + saml.EntityIdPath;
    }
}
