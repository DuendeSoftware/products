// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.ConformanceReport;

/// <summary>
/// Supplies ConformanceReport server options from the IdentityServer configuration.
/// </summary>
#pragma warning disable CA1812 // IdentityServerServerOptionsProvider is instantiated via DI
internal sealed class IdentityServerServerOptionsProvider(
    IOptions<IdentityServerOptions> options,
    IRedirectUriValidator redirectUriValidator,
    IKeyMaterialService keyMaterial) : IConformanceReportServerOptionsProvider
#pragma warning restore CA1812
{
    public async Task<ConformanceReportServerOptions> GetServerOptionsAsync(Ct ct)
    {
        // Loopback redirection for native clients is opted in to by registering
        // StrictRedirectUriValidatorAppAuth (AddAppAuthRedirectUriValidator). A fully
        // custom validator is reported as "loopback not enabled" (conservative).
        var loopbackEnabled = redirectUriValidator is StrictRedirectUriValidatorAppAuth;

        // IKeyMaterialService covers both static signing credentials and automatic key
        // management, which configuration alone cannot.
        var credentials = await keyMaterial.GetAllSigningCredentialsAsync(ct);
        var algorithms = credentials.Select(c => c.Algorithm).Distinct().ToList();

        return options.Value.ToConformanceReportServerOptions(loopbackEnabled, algorithms);
    }
}
