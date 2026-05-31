// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.Storage.Pagination;
using Duende.UserManagement;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.Logging;

using StorageQueryResult = Duende.Storage.Querying.QueryResult<Duende.UserManagement.Membership.RoleListItem>;

namespace Duende.IdentityServer.UserManagement;

/// <summary>
/// IProfileService implementation that integrates with Duende UserManagement.
/// </summary>
public class UserManagementProfileService : IProfileService
{
    private readonly IUserProfileAdmin? _userProfileAdmin;
    private readonly IMembershipAdmin? _membershipAdmin;

    /// <summary>
    /// The logger.
    /// </summary>
    protected ILogger<UserManagementProfileService> Logger { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManagementProfileService"/> class.
    /// </summary>
    public UserManagementProfileService(
        ILogger<UserManagementProfileService> logger,
        IUserProfileAdmin? userProfileAdmin = null,
        IMembershipAdmin? membershipAdmin = null
        )
    {
        _userProfileAdmin = userProfileAdmin;
        _membershipAdmin = membershipAdmin;
        Logger = logger;
    }

    /// <inheritdoc/>
    public virtual async Task GetProfileDataAsync(ProfileDataRequestContext context, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var activity = Tracing.ServiceActivitySource.StartActivity("UserManagementProfileService.GetProfileData");

        context.LogProfileRequest(Logger);

        if (_userProfileAdmin == null)
        {
            // Profiles not registered — fall back to default behavior (claims from subject).
            context.AddRequestedClaims(context.Subject.Claims);
            context.LogIssuedClaims(Logger);
            return;
        }

        var sub = context.Subject?.GetSubjectId();
        if (sub == null)
        {
            Logger.NoSubClaimPresent();
            return;
        }

        if (!UserSubjectId.TryCreate(sub, out var subjectId))
        {
            Logger.SubjectIdNotValid(sub);
            return;
        }

        await GetProfileDataAsync(context, subjectId, ct);
        context.LogIssuedClaims(Logger);
    }

    /// <summary>
    /// Gets profile data for the given subject ID.
    /// </summary>
    protected virtual async Task GetProfileDataAsync(ProfileDataRequestContext context, UserSubjectId subjectId, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(subjectId);

        var profile = await FindUserAsync(subjectId, ct);
        if (profile == null)
        {
            Logger.NoUserProfileFound(subjectId.ToString());
            return;
        }

        await GetProfileDataAsync(context, profile, ct);
    }

    /// <summary>
    /// Gets profile data for the given user profile.
    /// </summary>
    protected virtual async Task GetProfileDataAsync(ProfileDataRequestContext context, UserProfile profile, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var claims = new List<Claim>();

        foreach (var attribute in profile.Attributes.Values)
        {
            string value;
            string valueType;

            if (attribute.UntypedValue is bool boolValue)
            {
                value = boolValue ? "true" : "false";
                valueType = ClaimValueTypes.Boolean;
            }
            else
            {
                value = attribute.UntypedValue?.ToString() ?? string.Empty;
                valueType = ClaimValueTypes.String;
            }

            claims.Add(new Claim(attribute.Code.ToString(), value, valueType));
        }

        claims.AddRange(await GetRoleClaimsAsync(context, profile.SubjectId, ct));

        context.AddRequestedClaims(claims);
    }

    /// <inheritdoc/>
    public virtual async Task IsActiveAsync(IsActiveContext context, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var activity = Tracing.ServiceActivitySource.StartActivity("UserManagementProfileService.IsActive");

        Logger.IsActiveCalled(context.Caller);

        if (_userProfileAdmin == null)
        {
            // Profiles not registered — fall back to default behavior (always active).
            context.IsActive = true;
            return;
        }

        var sub = context.Subject?.GetSubjectId();
        if (sub == null)
        {
            context.IsActive = false;
            return;
        }

        if (!UserSubjectId.TryCreate(sub, out var subjectId))
        {
            Logger.SubjectIdNotValidInactive(sub);
            context.IsActive = false;
            return;
        }

        var profile = await FindUserAsync(subjectId, ct);
        context.IsActive = profile != null;
    }

    /// <summary>
    /// Loads the user profile by subject ID.
    /// </summary>
    protected virtual Task<UserProfile?> FindUserAsync(UserSubjectId subjectId, Ct ct) =>
        _userProfileAdmin!.TryGetAsync(subjectId, ct);

    /// <summary>
    /// Gets role claims for the specified user.
    /// </summary>
    protected virtual async Task<IEnumerable<Claim>> GetRoleClaimsAsync(ProfileDataRequestContext context, UserSubjectId subjectId, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_membershipAdmin == null)
        {
            return [];
        }

        if (!context.RequestedClaimTypes.Contains(JwtClaimTypes.Role))
        {
            return [];
        }

        var directTask = GetAllRolesAsync((range, token) => _membershipAdmin.GetDirectRolesAsync(subjectId, range, token), ct);
        var transitiveTask = GetAllRolesAsync((range, token) => _membershipAdmin.GetTransitiveRolesAsync(subjectId, range, token), ct);

        _ = await Task.WhenAll(directTask, transitiveTask);

        var direct = await directTask;
        var transitive = await transitiveTask;

        return direct
            .Concat(transitive)
            .DistinctBy(r => r.Id)
            .Select(r => new Claim(JwtClaimTypes.Role, r.Name.ToString()));
    }

    private static async Task<List<RoleListItem>> GetAllRolesAsync(
        Func<DataRange?, Ct, Task<StorageQueryResult>> fetch, Ct ct)
    {
        var all = new List<RoleListItem>();
        var range = DataRange.FromPage((PageNumber)1);
        StorageQueryResult result;

        do
        {
            result = await fetch(range, ct);
            all.AddRange(result.Items);

            if (result.HasMoreData && result.NextToken != null)
            {
                range = DataRange.FromContinuationToken(result.NextToken);
            }
            else
            {
                break;
            }
        } while (result.HasMoreData);

        return all;
    }
}
