// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// A user session
/// </summary>
public class ServerSideSession
{
    /// <summary>
    /// The key
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// The cookie handler scheme
    /// </summary>
    public string Scheme { get; set; } = default!;

    /// <summary>
    /// The subject ID
    /// </summary>
    public string SubjectId { get; set; } = default!;

    /// <summary>
    /// The session ID
    /// </summary>
    public string SessionId { get; set; } = default!;

    /// <summary>
    /// The display name for the user
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The creation time
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// The renewal time
    /// </summary>
    public DateTime Renewed { get; set; }

    /// <summary>
    /// The expiration time
    /// </summary>
    public DateTime? Expires { get; set; }

    /// <summary>
    /// The serialized ticket
    /// </summary>
    public string Ticket { get; set; } = default!;

    /// <summary>
    /// The version of the stored session at the time this instance was read. <see cref="Stores.IServerSideSessionStore.GetSessionAsync"/>
    /// and <see cref="Stores.IServerSideSessionStore.GetSessionsAsync"/> set it.
    /// <see cref="Stores.IServerSideSessionStore.UpdateSessionAsync"/> saves the session only while this is still the stored version.
    /// 0 means the session was not read from the store, and the save is not checked.
    /// </summary>
    public int Version { get; set; }
}
