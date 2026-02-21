// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// This interface allows IdentityServer to connect to your user and profile store.
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// This method is called whenever claims about the user are requested (e.g. during token creation or via the userinfo endpoint)
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task GetProfileDataAsync(ProfileDataRequestContext context, CT ct);

    /// <summary>
    /// This method gets called whenever identity server needs to determine if the user is valid or active (e.g. if the user's account has been deactivated since they logged in).
    /// (e.g. during token issuance or validation).
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task IsActiveAsync(IsActiveContext context, CT ct);
}
