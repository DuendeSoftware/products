// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default client configuration validator
/// </summary>
/// <seealso cref="IClientConfigurationValidator" />
public class DefaultClientConfigurationValidator : IClientConfigurationValidator
{
    private readonly IdentityServerOptions _options;

    /// <summary>
    /// Constructor for DefaultClientConfigurationValidator
    /// </summary>
    public DefaultClientConfigurationValidator(IdentityServerOptions options) => _options = options;

    /// <summary>
    /// Determines whether the configuration of a client is valid.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public async Task ValidateAsync(ClientConfigurationValidationContext context, CT ct)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("DefaultClientConfigurationValidator.Validate");

        if (context.Client.ProtocolType == IdentityServerConstants.ProtocolTypes.OpenIdConnect)
        {
            await ValidateGrantTypesAsync(context);
            if (context.IsValid == false)
            {
                return;
            }

            await ValidateLifetimesAsync(context);
            if (context.IsValid == false)
            {
                return;
            }

            await ValidateRedirectUriAsync(context);
            if (context.IsValid == false)
            {
                return;
            }

            await ValidateAllowedCorsOriginsAsync(context);
            if (context.IsValid == false)
            {
                return;
            }

            await ValidateUriSchemesAsync(context);
            if (context.IsValid == false)
            {
                return;
            }

            await ValidateSecretsAsync(context);
            if (context.IsValid == false)
            {
                return;
            }

            await ValidatePropertiesAsync(context);
            if (context.IsValid == false)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Validates grant type related configuration settings.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    protected virtual Task ValidateGrantTypesAsync(ClientConfigurationValidationContext context)
    {
        if (context.Client.AllowedGrantTypes?.Count == 0)
        {
            context.SetError("no allowed grant type specified");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates lifetime related configuration settings.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    protected virtual Task ValidateLifetimesAsync(ClientConfigurationValidationContext context)
    {
        if (context.Client.AccessTokenLifetime <= 0)
        {
            context.SetError("access token lifetime is 0 or negative");
            return Task.CompletedTask;
        }

        if (context.Client.IdentityTokenLifetime <= 0)
        {
            context.SetError("identity token lifetime is 0 or negative");
            return Task.CompletedTask;
        }

        if (context.Client.AllowedGrantTypes?.Contains(GrantType.DeviceFlow) == true
            && context.Client.DeviceCodeLifetime <= 0)
        {
            context.SetError("device code lifetime is 0 or negative");
        }

        // 0 means unlimited lifetime
        if (context.Client.AbsoluteRefreshTokenLifetime < 0)
        {
            context.SetError("absolute refresh token lifetime is negative");
            return Task.CompletedTask;
        }

        // 0 might mean that sliding is disabled
        if (context.Client.SlidingRefreshTokenLifetime < 0)
        {
            context.SetError("sliding refresh token lifetime is negative");
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates redirect URI related configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    protected virtual Task ValidateRedirectUriAsync(ClientConfigurationValidationContext context)
    {
        if (context.Client.AllowedGrantTypes?.Count > 0)
        {
            if (context.Client.AllowedGrantTypes.Contains(GrantType.AuthorizationCode) ||
                context.Client.AllowedGrantTypes.Contains(GrantType.Hybrid) ||
                context.Client.AllowedGrantTypes.Contains(GrantType.Implicit))
            {
                // Clients must have redirect uris, unless the PAR option to use
                // unregistered pushed uris is enabled and the client is a
                // confidential client
                var allowedByPar = _options.PushedAuthorization.AllowUnregisteredPushedRedirectUris &&
                    context.Client.RequireClientSecret;

                if (context.Client.RedirectUris?.Count == 0 &&
                    !allowedByPar)
                {
                    context.SetError("No redirect URI configured.");
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates allowed CORS origins for valid format.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    protected virtual Task ValidateAllowedCorsOriginsAsync(ClientConfigurationValidationContext context)
    {
        if (context.Client.AllowedCorsOrigins.Count > 0)
        {
            foreach (var origin in context.Client.AllowedCorsOrigins)
            {
                var fail = true;

                if (!string.IsNullOrWhiteSpace(origin) && origin.IsUri())
                {
                    var uri = new Uri(origin);

                    if (uri.AbsolutePath == "/" && !origin.EndsWith('/'))
                    {
                        fail = false;
                    }
                }

                if (fail)
                {
                    if (!string.IsNullOrWhiteSpace(origin))
                    {
                        context.SetError($"AllowedCorsOrigins contains invalid origin: {origin}");
                    }
                    else
                    {
                        context.SetError($"AllowedCorsOrigins contains invalid origin. There is an empty value.");
                    }
                    return Task.CompletedTask;
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates that URI schemes is not in the list of invalid URI scheme prefixes, as controlled by the ValidationOptions.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Task ValidateUriSchemesAsync(ClientConfigurationValidationContext context)
    {
        if (context.Client.RedirectUris?.Count > 0)
        {
            foreach (var uri in context.Client.RedirectUris)
            {
                if (_options.Validation.InvalidRedirectUriPrefixes
                    .Any(scheme => uri?.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) == true))
                {
                    context.SetError($"RedirectUri '{uri}' uses invalid scheme. If this scheme should be allowed, then configure it via ValidationOptions.");
                }
            }
        }

        if (context.Client.PostLogoutRedirectUris?.Count > 0)
        {
            foreach (var uri in context.Client.PostLogoutRedirectUris)
            {
                if (_options.Validation.InvalidRedirectUriPrefixes
                    .Any(scheme => uri?.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) == true))
                {
                    context.SetError($"PostLogoutRedirectUri '{uri}' uses invalid scheme. If this scheme should be allowed, then configure it via ValidationOptions.");
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates secret related configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    protected virtual Task ValidateSecretsAsync(ClientConfigurationValidationContext context)
    {
        if (context.Client.AllowedGrantTypes?.Count > 0)
        {
            foreach (var grantType in context.Client.AllowedGrantTypes)
            {
                if (!string.Equals(grantType, GrantType.Implicit, StringComparison.Ordinal))
                {
                    if (context.Client.RequireClientSecret && context.Client.ClientSecrets.Count == 0)
                    {
                        context.SetError($"Client secret is required for {grantType}, but no client secret is configured.");
                        return Task.CompletedTask;
                    }
                }

                if (string.Equals(grantType, GrantType.ClientCredentials, StringComparison.Ordinal) && !context.Client.RequireClientSecret)
                {
                    context.SetError("RequireClientSecret is false, but client is using client credentials grant type.");
                    return Task.CompletedTask;
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates properties related configuration settings.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    protected virtual Task ValidatePropertiesAsync(ClientConfigurationValidationContext context) => Task.CompletedTask;
}
