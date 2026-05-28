// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Configuration.DependencyInjection;

/// <summary>
/// Sets <see cref="IdentityServerOptions.LicenseKey"/> from <see cref="IConfiguration"/> when
/// it has not already been set imperatively. Reads from <c>Duende:IdentityServer:LicenseKey</c>
/// first, falling back to <c>Duende:LicenseKey</c>.
/// </summary>
internal sealed class PostConfigureLicenseKey(IConfiguration configuration)
    : IPostConfigureOptions<IdentityServerOptions>
{
    public void PostConfigure(string? name, IdentityServerOptions options)
    {
        if (options.LicenseKey != null)
        {
            return;
        }

        var key = configuration["Duende:IdentityServer:LicenseKey"]
               ?? configuration["Duende:LicenseKey"];

        if (!string.IsNullOrWhiteSpace(key))
        {
            options.LicenseKey = key.Trim();
        }
    }
}
