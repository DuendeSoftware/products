// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Saml;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.SamlSigninState;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed partial class SamlSigninStateStore(
    SamlSigninStateRepository repository,
    ISamlSigninStateSerializer serializer,
    TimeProvider timeProvider,
    ILogger<SamlSigninStateStore> logger) : ISamlSigninStateStore
{
    /// <inheritdoc/>
    public async Task<Guid> StoreSigninRequestStateAsync(SamlAuthenticationState state, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlSigninStateStore.StoreSigninRequestState");

        if (state.ExpiresAtUtc == default)
        {
            throw new ArgumentException("ExpiresAtUtc must be set before storing SAML signin state.", nameof(state));
        }

        var id = UuidV7.New();
        var serializedState = serializer.Serialize(state);
        var dso = new SamlSigninStateDso.V1
        {
            SerializedState = serializedState,
            ExpiresAtUtcTicks = state.ExpiresAtUtc.ToUniversalTime().Ticks,
            ServiceProviderEntityId = state.ServiceProviderEntityId
        };
        var expiration = Expiration.AtAbsolute(new DateTimeOffset(state.ExpiresAtUtc.ToUniversalTime(), TimeSpan.Zero));

        var result = await repository.CreateAsync(id, dso, expiration, ct);

        if (result != CreateResult.Success)
        {
            LogStoreFailure(id.Value, result);
            throw new InvalidOperationException($"Could not store SAML signin state {id.Value}: {result}");
        }

        LogStored(id.Value);
        return id.Value;
    }

    /// <inheritdoc/>
    public async Task<SamlAuthenticationState?> RetrieveSigninRequestStateAsync(Guid stateId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlSigninStateStore.RetrieveSigninRequestState");

        var entry = await repository.TryReadByIdAsync(stateId, ct);

        if (entry is null)
        {
            LogNotFound(stateId);
            return null;
        }

        var expiresAtUtc = new DateTime(entry.Value.Dso.ExpiresAtUtcTicks, DateTimeKind.Utc);

        if (expiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            LogExpired(stateId);
            return null;
        }

        return serializer.Deserialize(entry.Value.Dso.SerializedState);
    }

    /// <inheritdoc/>
    public async Task UpdateSigninRequestStateAsync(Guid stateId, SamlAuthenticationState state, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlSigninStateStore.UpdateSigninRequestState");

        if (!UuidV7.TryValidate(stateId, out _))
        {
            LogInvalidId(stateId, "Update");
            return;
        }

        var entry = await repository.TryReadByIdAsync(stateId, ct);

        if (entry is null)
        {
            LogNotFoundForOperation(stateId, "Update");
            return;
        }

        var expiresAtUtc = new DateTime(entry.Value.Dso.ExpiresAtUtcTicks, DateTimeKind.Utc);

        if (expiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            LogExpiredCannotUpdate(stateId);
            return;
        }

        var serializedState = serializer.Serialize(state);
        var updatedDso = new SamlSigninStateDso.V1
        {
            SerializedState = serializedState,
            ExpiresAtUtcTicks = entry.Value.Dso.ExpiresAtUtcTicks,
            ServiceProviderEntityId = entry.Value.Dso.ServiceProviderEntityId
        };
        var expiration = Expiration.AtAbsolute(new DateTimeOffset(expiresAtUtc, TimeSpan.Zero));

        var result = await repository.UpdateAsync(UuidV7.From(stateId), updatedDso, entry.Value.Version, expiration, ct);

        if (result != UpdateResult.Success)
        {
            LogUpdateFailure(stateId, result);
        }
        else
        {
            LogUpdated(stateId);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveSigninRequestStateAsync(Guid stateId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("SamlSigninStateStore.RemoveSigninRequestState");

        if (!UuidV7.TryValidate(stateId, out _))
        {
            LogInvalidId(stateId, "Remove");
            return;
        }

        await repository.DeleteByIdAsync(stateId, ct);

        LogRemoved(stateId);
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to store SAML signin state {StateId}. Result: {CreateResult}")]
    private partial void LogStoreFailure(Guid stateId, CreateResult createResult);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Stored SAML signin state {StateId}")]
    private partial void LogStored(Guid stateId);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "SAML signin state {StateId} not found")]
    private partial void LogNotFound(Guid stateId);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "SAML signin state {StateId} has expired")]
    private partial void LogExpired(Guid stateId);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "SAML signin state {StateId} is not a valid UUIDv7, skipping {Operation}")]
    private partial void LogInvalidId(Guid stateId, string operation);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "SAML signin state {StateId} not found for {Operation}")]
    private partial void LogNotFoundForOperation(Guid stateId, string operation);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SAML signin state {StateId} expired, cannot update")]
    private partial void LogExpiredCannotUpdate(Guid stateId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to update SAML signin state {StateId}. Result: {UpdateResult}")]
    private partial void LogUpdateFailure(Guid stateId, UpdateResult updateResult);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Updated SAML signin state {StateId}")]
    private partial void LogUpdated(Guid stateId);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Removed SAML signin state {StateId}")]
    private partial void LogRemoved(Guid stateId);
}
