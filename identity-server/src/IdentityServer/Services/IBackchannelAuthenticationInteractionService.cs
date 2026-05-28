// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Provides services used by the user interface to communicate with IdentityServer for
/// Client-Initiated Backchannel Authentication (CIBA) login requests.
/// This service is available from the dependency injection system and is typically injected
/// as a constructor parameter into MVC controllers that implement the CIBA user interaction UI.
/// </summary>
public interface IBackchannelAuthenticationInteractionService
{
    /// <summary>
    /// Returns all pending CIBA login requests for the currently authenticated user.
    /// Use this to display a list of pending authentication requests that the user needs to approve or deny.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="BackchannelUserLoginRequest"/> objects representing
    /// the pending login requests awaiting the current user's action.
    /// </returns>
    Task<IReadOnlyCollection<BackchannelUserLoginRequest>> GetPendingLoginRequestsForCurrentUserAsync(Ct ct);

    /// <summary>
    /// Returns the CIBA login request identified by the given internal store identifier.
    /// Use this to retrieve the details of a specific pending request so the user can review and act on it.
    /// </summary>
    /// <param name="id">The internal store identifier of the backchannel login request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="BackchannelUserLoginRequest"/> for the given <paramref name="id"/>,
    /// or <c>null</c> if no matching request is found.
    /// </returns>
    Task<BackchannelUserLoginRequest?> GetLoginRequestByInternalIdAsync(string id, Ct ct);

    /// <summary>
    /// Completes the CIBA login request with the provided response for the current user or the subject passed.
    /// Setting scopes on the <see cref="CompleteBackchannelLoginRequest"/> grants the request;
    /// leaving scopes null or empty denies it.
    /// </summary>
    /// <param name="completionRequest">
    /// The completion request containing the internal request ID, the consented scopes,
    /// an optional description, and optionally an explicit subject and session ID.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    Task CompleteLoginRequestAsync(CompleteBackchannelLoginRequest completionRequest, Ct ct);
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
