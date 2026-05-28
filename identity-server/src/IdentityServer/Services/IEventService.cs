// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Events;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Provides the ability to raise IdentityServer events, such as successful or failed logins,
/// token issuance, and consent decisions. Events are dispatched to the registered
/// <see cref="IEventSink"/> for persistence or forwarding to external systems.
/// Use <see cref="CanRaiseEventType"/> to check whether a given event category is enabled
/// before constructing and raising an event.
/// </summary>
public interface IEventService
{
#pragma warning disable CA1030 // This is our own eventing and this name is appropriate here
    /// <summary>
    /// Raises the specified event and dispatches it to the registered <see cref="IEventSink"/>.
    /// </summary>
    /// <param name="evt">The event to raise.</param>
    /// <param name="ct">The cancellation token.</param>
    Task RaiseAsync(Event evt, Ct ct);
#pragma warning restore CA1030

    /// <summary>
    /// Indicates whether events of the specified type will be persisted by the current configuration.
    /// Use this to avoid constructing event objects for event categories that are disabled.
    /// </summary>
    /// <param name="evtType">The event category to check.</param>
    /// <returns>
    /// <c>true</c> if events of the given <paramref name="evtType"/> are enabled and will be
    /// forwarded to the <see cref="IEventSink"/>; otherwise <c>false</c>.
    /// </returns>
    bool CanRaiseEventType(EventTypes evtType);
}
