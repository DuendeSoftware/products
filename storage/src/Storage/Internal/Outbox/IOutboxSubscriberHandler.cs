// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Defines the handler contract for processing outbox events for a specific subscriber.
/// Implementations are registered in DI using keyed services, keyed by subscriber name.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public interface IOutboxSubscriberHandler
{
    /// <summary>Handles a single outbox event delivered to this subscriber.</summary>
    /// <param name="item">The outbox event to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="HandleOutcomeResult"/> indicating how the job should proceed:
    /// <list type="bullet">
    ///   <item><see cref="HandleOutcomeResult.Success()"/> — event processed; will be deleted.</item>
    ///   <item><see cref="HandleOutcomeResult.Retry(string)"/> — transient failure; retry after a delay.</item>
    ///   <item><see cref="HandleOutcomeResult.Drop(string)"/> — permanent failure; delete without retry.</item>
    /// </list>
    /// Unhandled exceptions are treated as <see cref="HandleOutcomeResult.Retry(string)"/>.
    /// </returns>
    Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct);
}
