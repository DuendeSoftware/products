// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
///  Provide services be used by the user interface to communicate with IdentityServer for backchannel authentication requests.
/// </summary>
public interface IBackchannelAuthenticationInteractionService
{
    /// <summary>
    /// Returns the pending login requests for the current user.
    /// </summary>
    Task<IEnumerable<BackchannelUserLoginRequest>> GetPendingLoginRequestsForCurrentUserAsync(CT ct);

    /// <summary>
    /// Returns the login request for the id.
    /// </summary>
    Task<BackchannelUserLoginRequest?> GetLoginRequestByInternalIdAsync(string id, CT ct);

    /// <summary>
    /// Completes the login request with the provided response for the current user or the subject passed.
    /// </summary>
    /// <param name="completionRequest">The completion request.</param>
    /// <param name="ct">The cancellation token.</param>
    Task CompleteLoginRequestAsync(CompleteBackchannelLoginRequest completionRequest, CT ct);
}

/// <summary>
/// Models the data needed for a user to complete a backchannel authentication request.
/// </summary>
public class CompleteBackchannelLoginRequest
{
    /// <summary>
    /// Ctor
    /// </summary>
    public CompleteBackchannelLoginRequest(string internalId) => InternalId = internalId ?? throw new ArgumentNullException(nameof(internalId));

    /// <summary>
    /// The internal store id for the request.
    /// </summary>
    public string InternalId { get; set; }

    /// <summary>
    /// Gets or sets the scope values consented to. 
    /// Setting any scopes grants the login request.
    /// Leaving the scopes null or empty denies the request.
    /// </summary>
    public IEnumerable<string>? ScopesValuesConsented { get; set; }

    /// <summary>
    /// Gets or sets the optional description to associate with the consent.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The subject for which the completion is being made.
    /// This allows more claims to be associated with the request that was identified on the backchannel authentication request.
    /// If not provided, then the IUserSession service will be consulting to obtain the current subject.
    /// </summary>
    public ClaimsPrincipal? Subject { get; set; }

    /// <summary>
    /// The session id to associate with the completion request if the Subject is provided.
    /// If the Subject is not provided, then this property is ignored in favor of the session id provided by the IUserSession service.
    /// </summary>
    public string? SessionId { get; set; }
}
