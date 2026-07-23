// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.Storage;
using Duende.Storage.Internal.Operations;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.PushedAuthorization;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class PushedAuthorizationStore(
    PushedAuthorizationRepository repository,
    ILogger<PushedAuthorizationStore> logger) : IPushedAuthorizationRequestStore
{
    /// <inheritdoc/>
    public async Task StoreAsync(PushedAuthorizationRequest pushedAuthorizationRequest, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("PushedAuthorizationStore.Store");

        var dso = new PushedAuthorizationDso.V1
        {
            ReferenceValueHash = pushedAuthorizationRequest.ReferenceValueHash,
            Parameters = pushedAuthorizationRequest.Parameters,
            ExpiresAtUtcTicks = pushedAuthorizationRequest.ExpiresAtUtc.Ticks
        };

        var expiration = Duende.Storage.Internal.Expiration.AtAbsolute(
            new DateTimeOffset(DateTime.SpecifyKind(pushedAuthorizationRequest.ExpiresAtUtc, DateTimeKind.Utc)));

        var result = await repository.CreateAsync(UuidV7.New(), dso, expiration, ct);

        if (result != CreateResult.Success)
        {
            logger.LogError(
                "Failed to store pushed authorization request {Hash}: {Result}",
                pushedAuthorizationRequest.ReferenceValueHash,
                result);
            throw new InvalidOperationException(
                $"Could not store pushed authorization request: {result}");
        }
    }

    /// <inheritdoc/>
    public async Task<PushedAuthorizationRequest?> GetByHashAsync(string referenceValueHash, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("PushedAuthorizationStore.Get");

        var dso = await repository.TryReadByHashAsync(referenceValueHash, ct);
        if (dso is null)
        {
            logger.LogDebug("Pushed authorization request {Hash} not found in store", referenceValueHash);
            return null;
        }

        return new PushedAuthorizationRequest
        {
            ReferenceValueHash = dso.ReferenceValueHash,
            Parameters = dso.Parameters,
            ExpiresAtUtc = new DateTime(dso.ExpiresAtUtcTicks, DateTimeKind.Utc)
        };
    }

    /// <inheritdoc/>
    public async Task ConsumeByHashAsync(string referenceValueHash, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("PushedAuthorizationStore.Consume");

        logger.LogDebug("Consuming pushed authorization request {Hash}", referenceValueHash);

        await repository.DeleteByHashAsync(referenceValueHash, ct);
    }
}
