// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Test;

/// <summary>
/// Profile service for test users
/// </summary>
/// <seealso cref="IProfileService" />
public class TestUserProfileService : IProfileService
{
    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The users
    /// </summary>
    protected readonly TestUserStore Users;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestUserProfileService"/> class.
    /// </summary>
    /// <param name="users">The users.</param>
    /// <param name="logger">The logger.</param>
    public TestUserProfileService(TestUserStore users, ILogger<TestUserProfileService> logger)
    {
        Users = users;
        Logger = logger;
    }

    /// <summary>
    /// This method is called whenever claims about the user are requested (e.g. during token creation or via the userinfo endpoint)
    /// </summary>
    /// <param name="context">The context.</param>
    public virtual Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        context.LogProfileRequest(Logger);

        if (context.RequestedClaimTypes.Any())
        {
            var user = Users.FindBySubjectId(context.Subject.GetSubjectId());
            if (user != null)
            {
                context.AddRequestedClaims(user.Claims);
            }
        }

        context.LogIssuedClaims(Logger);

        return Task.CompletedTask;
    }

    /// <summary>
    /// This method gets called whenever identity server needs to determine if the user is valid or active (e.g. if the user's account has been deactivated since they logged in).
    /// (e.g. during token issuance or validation).
    /// </summary>
    /// <param name="context">The context.</param>
    public virtual Task IsActiveAsync(IsActiveContext context)
    {
        Logger.LogDebug("IsActive called from: {caller}", context.Caller);

        var user = Users.FindBySubjectId(context.Subject.GetSubjectId());
        context.IsActive = user?.IsActive == true;

        return Task.CompletedTask;
    }
}
