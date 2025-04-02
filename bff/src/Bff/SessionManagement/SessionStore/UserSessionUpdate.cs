// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// The data that can be updated for a user session
/// </summary>
public class UserSessionUpdate
{
    /// <summary>
    /// The subject ID
    /// </summary>
    public required string SubjectId { get; set; }

    /// <summary>
    /// The session ID
    /// </summary>
    public required string SessionId { get; set; }

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
    public required string Ticket { get; set; }

    /// <summary>
    /// Copies this instance into another
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public void CopyTo(UserSessionUpdate other)
    {
        other.SubjectId = SubjectId;
        other.SessionId = SessionId;
        other.Created = Created;
        other.Renewed = Renewed;
        other.Expires = Expires;
        other.Ticket = Ticket;
    }
}
