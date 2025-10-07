// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    /// <param name="profileServices">Profile services for configuration profile validation.</param>
    /// <param name="logger">Logger for configuration validation.</param>
    public ClientConfigurationValidator(IdentityServerOptions isOptions, KeyManagementOptions keyManagerOptions = null, IEnumerable<IConfigurationProfileService> profileServices = null, ILogger<DefaultClientConfigurationValidator> logger = null)
        : base(isOptions, profileServices ?? Enumerable.Empty<IConfigurationProfileService>(), logger ?? NullLogger<DefaultClientConfigurationValidator>.Instance) => _keyManagerOptions = keyManagerOptions;

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
