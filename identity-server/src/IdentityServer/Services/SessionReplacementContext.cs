// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Context describing a server-side session replacement.
/// </summary>
public sealed class SessionReplacementContext
{
    /// <summary>
    /// The subject id of the session that is being replaced, taken from the session record.
    /// </summary>
    public required string PreviousSubjectId { get; init; }

    /// <summary>
    /// The session id of the session that is being replaced, taken from the session record.
    /// </summary>
    public required string PreviousSessionId { get; init; }

    /// <summary>
    /// The session that is being replaced. This is null when the previous session's ticket
    /// could not be read, for example after the data protection keys used to protect it were
    /// lost. <see cref="PreviousSubjectId"/> and <see cref="PreviousSessionId"/> are always
    /// available, even when this is null.
    /// </summary>
    public UserSession? PreviousSession { get; init; }

    /// <summary>
    /// The subject id of the new session.
    /// </summary>
    public required string NewSubjectId { get; init; }

    /// <summary>
    /// The session id of the new session.
    /// </summary>
    public required string NewSessionId { get; init; }
}
