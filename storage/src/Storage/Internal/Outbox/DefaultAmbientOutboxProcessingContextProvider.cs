// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Default <see cref="IAmbientOutboxProcessingContextProvider"/>: establishes no additional
/// ambient context. Products with ambient context requirements (e.g. Spaces establishing
/// space context via <c>ISpaceContextAccessor.SetSpace</c>, which implies pool) replace this
/// registration.
/// </summary>
internal sealed class DefaultAmbientOutboxProcessingContextProvider : IAmbientOutboxProcessingContextProvider
{
    public Task<HandleOutcomeResult> ExecuteWithContextAsync(
        PersistedOutboxEvent evt,
        Func<Ct, Task<HandleOutcomeResult>> handlerAsync,
        Ct ct) =>
        handlerAsync(ct);
}
