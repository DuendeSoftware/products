// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal.Storage;
using Duende.Storage;
using Duende.Storage.Internal.Outbox;

namespace Duende.Spaces.Internal;

internal sealed class SpaceAwareAmbientOutboxProcessingContext(
    ISpaceStore spaceStore,
    ISpaceContextAccessor spaceContextAccessor)
    : IAmbientOutboxProcessingContextProvider
{
    /// <summary>
    /// Resolves the <see cref="Space"/> for <paramref name="evt"/>'s <c>PoolId</c>, establishes it
    /// as the ambient space context, invokes <paramref name="handlerAsync"/> while that context is
    /// active, and restores the prior context before returning.
    /// </summary>
    /// <remarks>
    /// The resolve, the <see cref="ISpaceContextAccessor.SetSpace"/> call, and the
    /// <paramref name="handlerAsync"/> invocation all happen within this single async method, even
    /// though the resolve can genuinely suspend on a cache miss. This is required, not incidental:
    /// see <see cref="IAmbientOutboxProcessingContextProvider"/>'s remarks for why establishing
    /// ambient state and invoking the handler cannot be split across a call boundary back to the
    /// caller (<c>OutboxProcessor</c>) without losing the ambient mutation.
    /// </remarks>
    public async Task<HandleOutcomeResult> ExecuteWithContextAsync(
        PersistedOutboxEvent evt,
        Func<Ct, Task<HandleOutcomeResult>> handlerAsync,
        Ct ct)
    {
        SpaceId spaceId;
        if (evt.PoolId == PoolId.Default)
        {
            spaceId = SpaceId.Default;
        }
        else if (evt.PoolId == PoolId.Management)
        {
            spaceId = SpaceId.Management;
        }
        else
        {
            var space = await spaceStore.TryGetSpaceByPoolId(evt.PoolId, ct);
            if (space == null)
            {
                throw new InvalidOperationException($"Failed to find a Space for PoolId: {evt.PoolId}");
            }
            spaceId = space.Id;
        }

        using var __ = spaceContextAccessor.SetSpace(spaceId);
        return await handlerAsync(ct);
    }
}
