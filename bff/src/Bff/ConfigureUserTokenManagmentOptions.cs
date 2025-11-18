// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Configures the Duende.AccessTokenManagement's UserTokenManagementOptions
/// based on the BFF's options.
/// </summary>
public class ConfigureUserTokenManagementOptions : IConfigureOptions<UserTokenManagementOptions>
{
    private readonly BffOptions _bffOptions;

    /// <summary>
    /// Creates an instance of the <see cref="ConfigureUserTokenManagementOptions"/>
    /// class.
    /// </summary>
    /// <param name="bffOptions"></param>
    public ConfigureUserTokenManagementOptions(IOptions<BffOptions> bffOptions) => _bffOptions = bffOptions.Value;
    /// <inheritdoc/>
    public void Configure(UserTokenManagementOptions options) => options.DPoPJsonWebKey = _bffOptions.DPoPJsonWebKey;
}
