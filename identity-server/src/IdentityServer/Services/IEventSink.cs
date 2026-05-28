// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Events;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Handles the persistence or forwarding of IdentityServer events raised by <see cref="IEventService"/>.
/// Implement this interface to integrate IdentityServer's event stream with an external system
/// such as a logging framework, audit database, or SIEM solution.
/// </summary>
public interface IEventSink
{
    /// <summary>
    /// Persists or forwards the specified event to the underlying storage or external system.
    /// Called by <see cref="IEventService"/> for every event that passes the configured event filter.
    /// </summary>
    /// <param name="evt">The event to persist or forward.</param>
    /// <param name="ct">The cancellation token.</param>
    Task PersistAsync(Event evt, Ct ct);
}
