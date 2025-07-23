// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Events;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Interface for the event service
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Raises the specified event.
    /// </summary>
    /// <param name="evt">The event.</param>
#pragma warning disable CA1030 // This is our own eventing and this name is appropriate here
    Task RaiseAsync(Event evt);
#pragma warning restore CA1030

    /// <summary>
    /// Indicates if the type of event will be persisted.
    /// </summary>
    bool CanRaiseEventType(EventTypes evtType);
}
