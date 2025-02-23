// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Filter to query all user sessions
/// </summary>
public class SessionQuery
{
    /// <summary>
    /// The token indicating the prior results.
    /// </summary>
    public string? ResultsToken { get; set; }

    /// <summary>
    /// If true, requests the previous set of results relative to the ResultsToken, otherwise requests the next set of results relative to the ResultsToken.
    /// </summary>
    public bool RequestPriorResults { get; set; }

    /// <summary>
    /// The number requested to return
    /// </summary>
    public int CountRequested { get; set; }

    /// <summary>
    /// The subject ID used to filter the results.
    /// </summary>
    public string? SubjectId { get; init; }

    /// <summary>
    /// The session ID used to filter the results.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// The user display name used to filter the results.
    /// </summary>
    public string? DisplayName { get; init; }
}
