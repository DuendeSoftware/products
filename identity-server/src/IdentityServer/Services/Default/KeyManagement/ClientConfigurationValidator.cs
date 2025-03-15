// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Client configuration validator that ensures access token lifetimes are compatible with the key management options.
/// </summary>
public class ClientConfigurationValidator : DefaultClientConfigurationValidator
{
    private readonly KeyManagementOptions _keyManagerOptions;

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="isOptions"></param>
    /// <param name="keyManagerOptions"></param>
    public ClientConfigurationValidator(IdentityServerOptions isOptions, KeyManagementOptions keyManagerOptions = null)
        : base(isOptions)
    {
        _keyManagerOptions = keyManagerOptions;
    }

    /// <inheritdoc/>
    protected override async Task ValidateLifetimesAsync(ClientConfigurationValidationContext context)
    {
        await base.ValidateLifetimesAsync(context);

        if (context.IsValid)
        {
            if (_keyManagerOptions == null)
            {
                throw new System.Exception("KeyManagerOptions not configured.");
            }

            var keyMaxAge = (int)_keyManagerOptions.KeyRetirementAge.TotalSeconds;
            var accessTokenAge = context.Client.AccessTokenLifetime;
            if (keyMaxAge < accessTokenAge)
            {
                context.SetError("AccessTokenLifetime is greater than the IdentityServer signing key KeyRetirement value.");
            }
        }
    }
}
