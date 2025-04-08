// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.SessionManagement.SessionStore;

/// <summary>
/// Filter to query user sessions
/// </summary>
public sealed class UserSessionsFilter
{
    /// <summary>
    /// The subject ID
    /// </summary>
    public string? SubjectId { get; init; }

    /// <summary>
    /// The sesion ID
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Validates
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SubjectId) && string.IsNullOrWhiteSpace(SessionId))
        {
            throw new InvalidOperationException("SubjectId or SessionId is required.");
        }
    }
}
