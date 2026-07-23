// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.IdentityServer.Configuration;
using Duende.UserManagement.Scim;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.UserManagement;

/// <summary>
/// Post-configures <see cref="ScimOAuthOptions"/> to auto-resolve the authority
/// from <see cref="IdentityServerOptions.IssuerUri"/> when running in the IdentityServer
/// integration package and no explicit authority has been set.
/// </summary>
[Experimental(diagnosticId: "duende_experimental",
    Message = "SCIM support is experimental and may change in future releases.")]
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class ScimAuthorityPostConfigureOptions(IOptions<IdentityServerOptions> identityServerOptions)
#pragma warning restore CA1812
    : IPostConfigureOptions<ScimOAuthOptions>
{
    public void PostConfigure(string? name, ScimOAuthOptions options)
    {
        // Only auto-resolve if the user has not explicitly set Authority
        if (!string.IsNullOrWhiteSpace(options.Authority))
        {
            return;
        }

        var issuerUri = identityServerOptions.Value.IssuerUri;
        if (!string.IsNullOrWhiteSpace(issuerUri))
        {
            options.Authority = issuerUri;
        }
    }
}
