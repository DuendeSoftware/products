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
    public string Key { get; set; } = null!;

    /// <summary>
    /// The cookie handler scheme
    /// </summary>
    public string Scheme { get; set; } = null!;

    /// <summary>
    /// The subject ID
    /// </summary>
    public string SubjectId { get; set; } = null!;

    /// <summary>
    /// The session ID
    /// </summary>
    public string SessionId { get; set; } = null!;

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
    public string Ticket { get; set; } = null!;
}
