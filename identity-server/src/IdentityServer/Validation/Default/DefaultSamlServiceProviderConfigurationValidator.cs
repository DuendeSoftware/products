// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default SAML service provider configuration validator.
/// </summary>
/// <seealso cref="ISamlServiceProviderConfigurationValidator" />
public class DefaultSamlServiceProviderConfigurationValidator : ISamlServiceProviderConfigurationValidator
{
    /// <summary>
    /// Determines whether the configuration of a SAML service provider is valid.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public async Task ValidateAsync(SamlServiceProviderConfigurationValidationContext context, Ct ct)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("DefaultSamlServiceProviderConfigurationValidator.Validate");

        await ValidateEntityIdAsync(context);
        if (context.IsValid == false)
        {
            return;
        }

        await ValidateAssertionConsumerServiceUrlsAsync(context);
        if (context.IsValid == false)
        {
            return;
        }

        await ValidateAllowedScopesAsync(context);
        if (context.IsValid == false)
        {
            return;
        }

        await ValidateLifetimesAsync(context);
    }

    /// <summary>
    /// Validates that the EntityId is not null or empty.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    protected virtual Task ValidateEntityIdAsync(SamlServiceProviderConfigurationValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ServiceProvider.EntityId))
        {
            context.SetError("EntityId is required");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates that at least one Assertion Consumer Service URL is configured.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    protected virtual Task ValidateAssertionConsumerServiceUrlsAsync(SamlServiceProviderConfigurationValidationContext context)
    {
        if (context.ServiceProvider.AssertionConsumerServiceUrls is not { Count: > 0 })
        {
            context.SetError("at least one Assertion Consumer Service URL is required");
            return Task.CompletedTask;
        }

        foreach (var acs in context.ServiceProvider.AssertionConsumerServiceUrls)
        {
            if (acs.Binding != SamlBinding.HttpPost)
            {
                context.SetError(
                    $"Assertion Consumer Service at index {acs.Index} uses an unsupported binding '{acs.Binding}'. " +
                    "Only HTTP-POST is supported for SAML Response delivery.");
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates that at least one allowed scope is configured.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    protected virtual Task ValidateAllowedScopesAsync(SamlServiceProviderConfigurationValidationContext context)
    {
        if (context.ServiceProvider.AllowedScopes is not { Count: > 0 })
        {
            context.SetError("at least one allowed scope is required");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates lifetime-related configuration settings.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    protected virtual Task ValidateLifetimesAsync(SamlServiceProviderConfigurationValidationContext context)
    {
        if (context.ServiceProvider.AssertionLifetime.HasValue && context.ServiceProvider.AssertionLifetime.Value <= TimeSpan.Zero)
        {
            context.SetError("AssertionLifetime must be positive");
            return Task.CompletedTask;
        }

        if (context.ServiceProvider.ClockSkew.HasValue && context.ServiceProvider.ClockSkew.Value < TimeSpan.Zero)
        {
            context.SetError("ClockSkew must be non-negative");
            return Task.CompletedTask;
        }

        if (context.ServiceProvider.RequestMaxAge.HasValue && context.ServiceProvider.RequestMaxAge.Value <= TimeSpan.Zero)
        {
            context.SetError("RequestMaxAge must be positive");
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
