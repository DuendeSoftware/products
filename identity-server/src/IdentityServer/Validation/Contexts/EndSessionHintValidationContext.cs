// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Context for validating the id_token_hint's sub/sid claims against the current user session
/// during end session (logout) requests.
/// </summary>
public class EndSessionHintValidationContext
{
    /// <summary>
    /// Gets the currently authenticated user (from the active session).
    /// </summary>
    public ClaimsPrincipal Subject { get; }

    /// <summary>
    /// Gets the result of validating the id_token_hint, including all claims from the token
    /// (e.g. <c>sub</c>, <c>sid</c>) and the associated client.
    /// </summary>
    public TokenValidationResult TokenValidationResult { get; }

    /// <summary>
    /// Gets the current session identifier, as returned by <c>IUserSession.GetSessionIdAsync</c>.
    /// May be <c>null</c> if the session does not have a session ID.
    /// </summary>
    public string? SessionId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EndSessionHintValidationContext"/> class.
    /// </summary>
    /// <param name="subject">The currently authenticated user.</param>
    /// <param name="tokenValidationResult">The result of validating the id_token_hint.</param>
    /// <param name="sessionId">The current session identifier.</param>
    public EndSessionHintValidationContext(
        ClaimsPrincipal subject,
        TokenValidationResult tokenValidationResult,
        string? sessionId)
    {
        Subject = subject;
        TokenValidationResult = tokenValidationResult;
        SessionId = sessionId;
    }
}
