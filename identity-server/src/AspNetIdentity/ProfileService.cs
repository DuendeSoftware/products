// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.AspNetIdentity;

/// <summary>
/// IProfileService to integrate with ASP.NET Identity.
/// </summary>
/// <typeparam name="TUser">The type of the user.</typeparam>
/// <seealso cref="IProfileService" />
public class ProfileService<TUser> : IProfileService
    where TUser : class
{
    /// <summary>
    /// The claims factory.
    /// </summary>
    protected readonly IUserClaimsPrincipalFactory<TUser> ClaimsFactory;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger<ProfileService<TUser>> Logger;

    /// <summary>
    /// The user manager.
    /// </summary>
    protected readonly UserManager<TUser> UserManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileService{TUser}"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="claimsFactory">The claims factory.</param>
    public ProfileService(UserManager<TUser> userManager,
        IUserClaimsPrincipalFactory<TUser> claimsFactory)
    {
        UserManager = userManager;
        ClaimsFactory = claimsFactory;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileService{TUser}"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="claimsFactory">The claims factory.</param>
    /// <param name="logger">The logger.</param>
    public ProfileService(UserManager<TUser> userManager,
        IUserClaimsPrincipalFactory<TUser> claimsFactory,
        ILogger<ProfileService<TUser>> logger)
    {
        UserManager = userManager;
        ClaimsFactory = claimsFactory;
        Logger = logger;
    }

    /// <summary>
    /// This method is called whenever claims about the user are requested (e.g. during token creation or via the userinfo endpoint)
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual async Task GetProfileDataAsync(ProfileDataRequestContext context, CT ct)
    {
        var sub = context.Subject?.GetSubjectId();
        if (sub == null)
        {
            throw new Exception("No sub claim present");
        }

        await GetProfileDataAsync(context, sub);
    }

    /// <summary>
    /// Called to get the claims for the subject based on the profile request.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="subjectId"></param>
    /// <returns></returns>
    protected virtual async Task GetProfileDataAsync(ProfileDataRequestContext context, string subjectId)
    {
        var user = await FindUserAsync(subjectId);
        if (user != null)
        {
            await GetProfileDataAsync(context, user);
        }
    }

    /// <summary>
    /// Called to get the claims for the user based on the profile request.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    protected virtual async Task GetProfileDataAsync(ProfileDataRequestContext context, TUser user)
    {
        var principal = await GetUserClaimsAsync(user);
        context.AddRequestedClaims(principal.Claims);
    }

    /// <summary>
    /// Gets the claims for a user.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    protected virtual async Task<ClaimsPrincipal> GetUserClaimsAsync(TUser user)
    {
        var principal = await ClaimsFactory.CreateAsync(user);
        if (principal == null)
        {
            throw new Exception("ClaimsFactory failed to create a principal");
        }

        return principal;
    }

    /// <summary>
    /// This method gets called whenever identity server needs to determine if the user is valid or active (e.g. if the user's account has been deactivated since they logged in).
    /// (e.g. during token issuance or validation).
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual async Task IsActiveAsync(IsActiveContext context, CT ct)
    {
        var sub = context.Subject?.GetSubjectId();
        if (sub == null)
        {
            throw new Exception("No subject Id claim present");
        }

        await IsActiveAsync(context, sub);
    }

    /// <summary>
    /// Determines if the subject is active.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="subjectId"></param>
    /// <returns></returns>
    protected virtual async Task IsActiveAsync(IsActiveContext context, string subjectId)
    {
        var user = await FindUserAsync(subjectId);
        if (user != null)
        {
            await IsActiveAsync(context, user);
        }
        else
        {
            context.IsActive = false;
        }
    }

    /// <summary>
    /// Determines if the user is active.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    protected virtual async Task IsActiveAsync(IsActiveContext context, TUser user) => context.IsActive = await IsUserActiveAsync(user);

    /// <summary>
    /// Returns if the user is active.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public virtual Task<bool> IsUserActiveAsync(TUser user) => Task.FromResult(true);

    /// <summary>
    /// Loads the user by the subject id.
    /// </summary>
    /// <param name="subjectId"></param>
    /// <returns></returns>
    protected virtual async Task<TUser> FindUserAsync(string subjectId)
    {
        var user = await UserManager.FindByIdAsync(subjectId);
        if (user == null)
        {
            Logger?.LogWarning("No user found matching subject Id: {subjectId}", subjectId);
        }

        return user;
    }
}
