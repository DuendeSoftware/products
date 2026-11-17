// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Services;
using Duende.Spaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Supplies <c>JwtBearerOptions.ConfigurationManager</c> for the SCIM bearer scheme
/// (<see cref="ScimConstants.AuthenticationScheme"/>) when no <c>Authority</c> has been configured.
/// Instead of a discovery document fetched over HTTP, this reads the current request's issuer
/// (<see cref="IIssuerNameService"/>) and signing keys (<see cref="IKeyMaterialService"/>) directly
/// from IdentityServer's own services, so a token is always validated against the space that is
/// serving the request.
/// </summary>
/// <remarks>
/// This type deliberately does <b>not</b> derive from <c>BaseConfigurationManager</c>. ASP.NET
/// Core's <c>JwtBearerHandler.SetupTokenValidationParametersAsync</c> only calls
/// <see cref="GetConfigurationAsync"/> once per request and copies <c>Issuer</c> and
/// <c>SigningKeys</c> into the token validation parameters for configuration managers that are
/// not a <c>BaseConfigurationManager</c>. A <c>BaseConfigurationManager</c>-derived implementation
/// would instead be cached by the framework, which would leak one space's issuer and keys into
/// another space's requests.
/// </remarks>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed partial class ScimLocalIssuerConfigurationManager(
    IHttpContextAccessor httpContextAccessor,
    ILogger<ScimLocalIssuerConfigurationManager> logger)
#pragma warning restore CA1812
    : IConfigurationManager<OpenIdConnectConfiguration>
{
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(Ct ct)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                $"{nameof(ScimLocalIssuerConfigurationManager)} requires an active {nameof(HttpContext)}.");

        var requestServices = httpContext.RequestServices;

        // With Spaces registered but no space set (for example UseSpaceResolution() placed after
        // UseAuthorization()), the issuer and keys below would come from the default space. Fail
        // closed instead: log an error and return an empty configuration, so the token fails
        // validation with a 401 rather than an exception surfacing as a 500.
        var spaceContextAccessor = requestServices.GetService<ISpaceContextAccessor>();
        if (spaceContextAccessor is not null && !spaceContextAccessor.IsSpaceIdConfigured())
        {
            Log.NoSpaceConfiguredForScimRequest(logger);
            return new OpenIdConnectConfiguration();
        }

        var issuerNameService = requestServices.GetRequiredService<IIssuerNameService>();
        var keyMaterialService = requestServices.GetRequiredService<IKeyMaterialService>();

        var issuer = await issuerNameService.GetCurrentAsync(ct);
        var validationKeys = await keyMaterialService.GetValidationKeysAsync(ct);

        var configuration = new OpenIdConnectConfiguration { Issuer = issuer };
        foreach (var key in validationKeys)
        {
            configuration.SigningKeys.Add(key.Key);
        }

        Log.ResolvedLocalScimIssuer(logger, issuer, validationKeys.Count);

        return configuration;
    }

    public void RequestRefresh()
    {
        // No cache to invalidate: a fresh configuration is resolved for every request.
    }
}
