// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

/// <summary>
/// Post-configures <see cref="Saml2Options"/> for use with the dynamic
/// provider infrastructure, setting defaults for the logger adapter and
/// cookie manager that are not covered by <see cref="SamlConfigureOptions"/>.
/// </summary>
internal sealed class PostConfigureSaml2OptionsForDynamic : IPostConfigureOptions<Saml2Options>
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Ctor
    /// </summary>
    public PostConfigureSaml2OptionsForDynamic(ILoggerFactory loggerFactory) =>
        _loggerFactory = loggerFactory;

    /// <inheritdoc/>
    public void PostConfigure(string? name, Saml2Options options)
    {
        // Wire up the Saml2 logger via the ASP.NET Core logging infrastructure
        if (options.SPOptions.Logger == null)
        {
            options.SPOptions.Logger = new AspNetCoreLoggerAdapter(
                _loggerFactory.CreateLogger<Saml2Handler>());
        }

        // Ensure a cookie manager is set for storing relay state
        options.CookieManager ??= new ChunkingCookieManager();
    }
}
