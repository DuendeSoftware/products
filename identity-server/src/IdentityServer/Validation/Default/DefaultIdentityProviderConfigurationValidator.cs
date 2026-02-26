// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default identity provider configuration validator
/// </summary>
/// <seealso cref="IIdentityProviderConfigurationValidator" />
public class DefaultIdentityProviderConfigurationValidator : IIdentityProviderConfigurationValidator
{
    private readonly IdentityServerOptions _options;

    /// <summary>
    /// Constructor for DefaultIdentityProviderConfigurationValidator
    /// </summary>
    public DefaultIdentityProviderConfigurationValidator(IdentityServerOptions options) => _options = options;

    /// <inheritdoc/>
    public virtual async Task ValidateAsync(IdentityProviderConfigurationValidationContext context, Ct ct)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("DefaultIdentityProviderConfigurationValidator.Validate");

        var type = _options.DynamicProviders.FindProviderType(context.IdentityProvider.Type);
        if (type == null)
        {
            context.SetError("IdentityProvider Type has not been registered with AddProviderType on the DynamicProviderOptions.");
            return;
        }

        if (string.IsNullOrWhiteSpace(context.IdentityProvider.Scheme))
        {
            context.SetError("Scheme is missing.");
            return;
        }

        if (context.IdentityProvider is OidcProvider oidc)
        {
            var oidcContext = new IdentityProviderConfigurationValidationContext<OidcProvider>(oidc);
            await ValidateOidcProviderAsync(oidcContext);

            if (!oidcContext.IsValid)
            {
                context.SetError(oidcContext.ErrorMessage);
            }

            return;
        }
    }

    /// <summary>
    /// Validates the OIDC identity provider.
    /// </summary>
    /// <returns>A string that represents the error. Null if there is no error.</returns>
    protected virtual Task ValidateOidcProviderAsync(IdentityProviderConfigurationValidationContext<OidcProvider> context)
    {
        if (string.IsNullOrWhiteSpace(context.IdentityProvider.Authority))
        {
            context.SetError("Authority is missing.");
        }

        if (string.IsNullOrWhiteSpace(context.IdentityProvider.ClientId))
        {
            context.SetError("ClientId is missing.");
        }

        if (string.IsNullOrWhiteSpace(context.IdentityProvider.ResponseType))
        {
            context.SetError("ResponseType is missing.");
        }

        if (string.IsNullOrWhiteSpace(context.IdentityProvider.Scope))
        {
            context.SetError("Scope is missing.");
        }

        return Task.CompletedTask;
    }
}
