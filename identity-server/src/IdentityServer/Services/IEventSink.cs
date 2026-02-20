// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Events;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Models persistence of events
/// </summary>
public interface IEventSink
{
    /// <summary>
    /// Raises the specified event.
    /// </summary>
    /// <param name="evt">The event.</param>
    /// <param name="ct">The cancellation token.</param>
    Task PersistAsync(Event evt, CT ct);
}
