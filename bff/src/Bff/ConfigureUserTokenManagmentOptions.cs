// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Configures the Duende.AccessTokenManagement's UserTokenManagementOptions
/// based on the BFF's options.
/// </summary>
public class ConfigureUserTokenManagementOptions(IOptions<BffOptions> bffOptions) : IConfigureOptions<UserTokenManagementOptions>
{
    /// <inheritdoc/>
    public void Configure(UserTokenManagementOptions options)
    {
        options.DPoPJsonWebKey = bffOptions.Value.DPoPJsonWebKey;
    }
}
