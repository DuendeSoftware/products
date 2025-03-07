// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Events;

/// <summary>
/// Indicates if the event is a success or fail event.
/// </summary>
public enum EventTypes
{
    /// <summary>
    /// Success event
    /// </summary>
    Success = 1,

    /// <summary>
    /// Failure event
    /// </summary>
    Failure = 2,

    /// <summary>
    /// Information event
    /// </summary>
    Information = 3,

    /// <summary>
    /// Error event
    /// </summary>
    Error = 4
}