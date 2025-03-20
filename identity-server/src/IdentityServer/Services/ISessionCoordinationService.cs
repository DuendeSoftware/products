// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Service responsible for logic around coordinating client and server session lifetimes.
/// </summary>
public interface ISessionCoordinationService
{
    /// <summary>
    /// Coordinates when a user logs out.
    /// </summary>
    Task ProcessLogoutAsync(UserSession session);

    /// <summary>
    /// Coordinates when a user session has expired.
    /// </summary>
    Task ProcessExpirationAsync(UserSession session);

    /// <summary>
    /// Validates client request, and if valid extends server-side session.
    /// Returns false if the session is invalid, true otherwise.
    /// </summary>
    Task<bool> ValidateSessionAsync(SessionValidationRequest request);
}

/// <summary>
/// Models request to validation a session from a client.
/// </summary>
public class SessionValidationRequest
{
    /// <summary>
    /// The subject ID
    /// </summary>
    public string SubjectId { get; set; } = null!;

    /// <summary>
    /// The session ID
    /// </summary>
    public string SessionId { get; set; } = null!;

    /// <summary>
    /// The client making the request.
    /// </summary>
    public Client Client { get; set; } = null!;

    /// <summary>
    /// Indicates the type of request.
    /// </summary>
    public SessionValidationType Type { get; set; }
}

/// <summary>
/// Represent the type of session validation request
/// </summary>
public enum SessionValidationType
{
    /// <summary>
    /// Refresh token use at token endpoint
    /// </summary>
    RefreshToken,
    /// <summary>
    /// Access token use by client at userinfo endpoint or at an API that uses introspection
    /// </summary>
    AccessToken,
}
