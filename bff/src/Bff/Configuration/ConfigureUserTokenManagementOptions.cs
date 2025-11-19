// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Configuration;

/// <summary>
/// Configures the Duende.AccessTokenManagement's UserTokenManagementOptions
/// based on the BFF's options.
/// </summary>
internal class ConfigureUserTokenManagementOptions(IOptions<BffOptions> bffOptions) : IConfigureOptions<UserTokenManagementOptions>
{
    /// <inheritdoc/>
    public void Configure(UserTokenManagementOptions options)
    {
        if (options.DPoPJsonWebKey == null)
        {
            options.DPoPJsonWebKey = bffOptions.Value.DPoPJsonWebKey;
        }
    }
}
