// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Threading.Tasks;
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
    Task RaiseAsync(Event evt);

    /// <summary>
    /// Indicates if the type of event will be persisted.
    /// </summary>
    bool CanRaiseEventType(EventTypes evtType);
}
