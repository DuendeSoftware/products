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
/// SAML service provider store decorator for runtime configuration validation.
/// </summary>
public class ValidatingSamlServiceProviderStore<T> : ISamlServiceProviderStore
    where T : ISamlServiceProviderStore
{
    private readonly ISamlServiceProviderStore _inner;
    private readonly ISamlServiceProviderConfigurationValidator _validator;
    private readonly IEventService _events;
    private readonly ILogger<ValidatingSamlServiceProviderStore<T>> _logger;
    private readonly string _validatorType;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatingSamlServiceProviderStore{T}"/> class.
    /// </summary>
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
        _validatorType = validator.GetType().FullName!;
    }

    /// <inheritdoc/>
    public async Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity(
            "ValidatingSamlServiceProviderStore.FindByEntityId");

        var sp = await _inner.FindByEntityIdAsync(entityId, ct);

        if (sp != null)
        {
            _logger.LogTrace("Calling into SAML SP configuration validator: {validatorType}", _validatorType);
            var context = new SamlServiceProviderConfigurationValidationContext(sp);
            await _validator.ValidateAsync(context);

            if (context.IsValid)
            {
                _logger.LogDebug("SAML SP configuration validation for {entityId} succeeded.", sp.EntityId);
                Telemetry.Metrics.SamlServiceProviderValidation(entityId);
                return sp;
            }

            _logger.LogError("Invalid SAML SP configuration for {entityId}: {errorMessage}",
                sp.EntityId, context.ErrorMessage);
            Telemetry.Metrics.SamlServiceProviderValidationFailure(entityId, context.ErrorMessage);
            await _events.RaiseAsync(
                new InvalidSamlServiceProviderConfigurationEvent(sp, context.ErrorMessage), ct);
            return null;
        }

        Telemetry.Metrics.SamlServiceProviderValidationFailure(entityId, "Service provider not found");
        return null;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync([EnumeratorCancellation] Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity(
            "ValidatingSamlServiceProviderStore.GetAllSamlServiceProviders");
        await foreach (var sp in _inner.GetAllSamlServiceProvidersAsync(ct))
        {
            _logger.LogTrace("Calling into SAML SP configuration validator: {validatorType}", _validatorType);
            var context = new SamlServiceProviderConfigurationValidationContext(sp);
            await _validator.ValidateAsync(context);
            if (context.IsValid)
            {
                _logger.LogDebug("SAML SP configuration validation for {entityId} succeeded.", sp.EntityId);
                Telemetry.Metrics.SamlServiceProviderValidation(sp.EntityId);
                yield return sp;
            }
            else
            {
                _logger.LogError("Invalid SAML SP configuration for {entityId}: {errorMessage}",
                    sp.EntityId, context.ErrorMessage);
                Telemetry.Metrics.SamlServiceProviderValidationFailure(sp.EntityId, context.ErrorMessage);
                await _events.RaiseAsync(
                    new InvalidSamlServiceProviderConfigurationEvent(sp, context.ErrorMessage), ct);
            }
        }
    }
}
