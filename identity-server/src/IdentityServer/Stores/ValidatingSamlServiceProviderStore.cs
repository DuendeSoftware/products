// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Runtime.CompilerServices;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// SAML service provider store decorator for running runtime configuration validation checks.
/// </summary>
public class ValidatingSamlServiceProviderStore<T> : ISamlServiceProviderStore
    where T : ISamlServiceProviderStore
{
    private readonly ISamlServiceProviderStore _inner;
    private readonly ISamlServiceProviderConfigurationValidator _validator;
    private readonly IEventService _events;
    private readonly ILogger<ValidatingSamlServiceProviderStore<T>> _logger;
    private readonly string? _validatorType;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatingSamlServiceProviderStore{T}"/> class.
    /// </summary>
    /// <param name="inner">The inner store.</param>
    /// <param name="validator">The validator.</param>
    /// <param name="events">The event service.</param>
    /// <param name="logger">The logger.</param>
    public ValidatingSamlServiceProviderStore(
        T inner,
        ISamlServiceProviderConfigurationValidator validator,
        IEventService events,
        ILogger<ValidatingSamlServiceProviderStore<T>> logger)
    {
        _inner = inner;
        _validator = validator;
        _events = events;
        _logger = logger;

        _validatorType = validator.GetType().FullName;
    }

    /// <summary>
    /// Finds a SAML service provider by entity ID (and runs the validation logic).
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The service provider, or null if not found or invalid.</returns>
    public async Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ValidatingSamlServiceProviderStore.FindByEntityId");

        var serviceProvider = await _inner.FindByEntityIdAsync(entityId, ct);

        if (serviceProvider != null)
        {
            _logger.LogTrace("Calling into SAML service provider configuration validator: {validatorType}", _validatorType);

            var context = new SamlServiceProviderConfigurationValidationContext(serviceProvider);
            await _validator.ValidateAsync(context, ct);

            if (context.IsValid)
            {
                _logger.LogDebug("SAML service provider configuration validation for {entityId} succeeded.", serviceProvider.EntityId);
                Telemetry.Metrics.SamlServiceProviderValidation(serviceProvider.EntityId);
                return serviceProvider;
            }

            _logger.LogError("Invalid SAML service provider configuration for {entityId}: {errorMessage}", serviceProvider.EntityId, context.ErrorMessage);
            Telemetry.Metrics.SamlServiceProviderValidationFailure(serviceProvider.EntityId, context.ErrorMessage ?? "Validation failed");
            await _events.RaiseAsync(new InvalidSamlServiceProviderConfigurationEvent(serviceProvider, context.ErrorMessage ?? "Validation failed"), ct);

            return null;
        }

        Telemetry.Metrics.SamlServiceProviderValidationFailure(entityId, "Service provider not found");

        return null;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync([EnumeratorCancellation] Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ValidatingSamlServiceProviderStore.GetAllSamlServiceProviders");
        await foreach (var serviceProvider in _inner.GetAllSamlServiceProvidersAsync(ct))
        {
            _logger.LogTrace("Calling into SAML service provider configuration validator: {validatorType}", _validatorType);
            var context = new SamlServiceProviderConfigurationValidationContext(serviceProvider);
            await _validator.ValidateAsync(context, ct);
            if (context.IsValid)
            {
                _logger.LogDebug("SAML service provider configuration validation for {entityId} succeeded.", serviceProvider.EntityId);
                Telemetry.Metrics.SamlServiceProviderValidation(serviceProvider.EntityId);
                yield return serviceProvider;
            }
            else
            {
                _logger.LogError("Invalid SAML service provider configuration for {entityId}: {errorMessage}", serviceProvider.EntityId, context.ErrorMessage);
                Telemetry.Metrics.SamlServiceProviderValidationFailure(serviceProvider.EntityId, context.ErrorMessage ?? "Validation failed");
                await _events.RaiseAsync(new InvalidSamlServiceProviderConfigurationEvent(serviceProvider, context.ErrorMessage ?? "Validation failed"), ct);
                // Skip invalid service providers - do not yield
            }
        }
    }
}
