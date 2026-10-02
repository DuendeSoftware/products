// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Outbox;

/// <summary>
/// Establishes whatever ambient context a handler needs to process a single outbox event
/// (at minimum, the event's pool), invokes the handler while that context is active, and
/// tears the context down again before returning.
/// </summary>
/// <remarks>
/// <para>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </para>
/// <para>
/// Establishing context and invoking <c>handlerAsync</c> must both happen inside this single
/// call, rather than the caller establishing context via one call and invoking the handler via
/// a separate, later call. Ambient context here is <see cref="AsyncLocal{T}"/>-backed (e.g.
/// Spaces' space/pool context); an implementation whose own <c>await</c> genuinely suspends
/// before it sets that ambient state creates a new <see cref="System.Threading.ExecutionContext"/>
/// branch that is only visible to code running further inside the same call. If the ambient
/// mutation were instead performed inside an <c>EstablishContext</c>-only call whose returned
/// <see cref="IDisposable"/> the caller applied afterward, the mutation would not be visible to the
/// caller once that call's <see cref="Task"/> completed, because .NET restores the caller's own
/// pre-await execution context on resume. Implementations must therefore invoke
/// <c>handlerAsync</c> themselves, from within the same call that establishes context.
/// </para>
/// </remarks>
public interface IAmbientOutboxProcessingContextProvider
{
    /// <summary>
    /// Establishes ambient context for <paramref name="evt"/>, invokes <paramref name="handlerAsync"/>
    /// while that context is active, and tears the context down again before returning.
    /// </summary>
    /// <param name="evt">The outbox event about to be dispatched to its handler.</param>
    /// <param name="handlerAsync">
    /// The handler invocation to run while the ambient context established for <paramref name="evt"/>
    /// is active. Exceptions raised by <paramref name="handlerAsync"/> are the caller's concern, not
    /// the implementation's: an implementation must let them propagate unmodified.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The outcome returned by <paramref name="handlerAsync"/>.</returns>
    Task<HandleOutcomeResult> ExecuteWithContextAsync(
        PersistedOutboxEvent evt,
        Func<Ct, Task<HandleOutcomeResult>> handlerAsync,
        Ct ct);
}
