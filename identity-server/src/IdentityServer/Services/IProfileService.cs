// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Allows IdentityServer to connect to your user and profile store.
/// Implement this interface to control which claims are included in tokens and at the UserInfo endpoint,
/// and to determine whether a user is currently allowed to obtain tokens (e.g. if the account has been deactivated).
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// Called whenever claims about the user are requested, for example during token creation
    /// or when the UserInfo endpoint is called. Implementations should populate
    /// <see cref="ProfileDataRequestContext.IssuedClaims"/> with the claims that should be included.
    /// </summary>
    /// <param name="context">
    /// The context describing the request, including the user's <c>Subject</c>, the requesting <c>Client</c>,
    /// the <c>RequestedClaimTypes</c> derived from the requested scopes, and the <c>Caller</c> identifier
    /// indicating whether the request originates from a token endpoint, UserInfo endpoint, etc.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    Task GetProfileDataAsync(ProfileDataRequestContext context, Ct ct);

    /// <summary>
    /// Called whenever IdentityServer needs to determine whether the user is valid or active,
    /// for example during token issuance or validation. Implementations should set
    /// <see cref="IsActiveContext.IsActive"/> to <c>false</c> if the user should not be
    /// allowed to obtain tokens (e.g. the account has been deactivated since the user logged in).
    /// </summary>
    /// <param name="context">
    /// The context describing the request, including the user's <c>Subject</c>, the requesting <c>Client</c>,
    /// and the <c>Caller</c> identifier. Set <c>context.IsActive</c> to indicate whether the user is allowed
    /// to obtain tokens.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    Task IsActiveAsync(IsActiveContext context, Ct ct);
}
