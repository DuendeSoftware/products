// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.Storage.Pagination;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.Logging;

using StorageQueryResult = Duende.Storage.Querying.QueryResult<Duende.UserManagement.Membership.RoleListItem>;

namespace Duende.IdentityServer.UserManagement;

/// <summary>
/// IProfileService implementation that integrates with Duende UserManagement.
/// </summary>
public class UserManagementProfileService(
    ILogger<UserManagementProfileService> logger,
    IUserAuthenticatorsAdmin authenticatorsAdmin,
    IUserProfileAdmin profileAdmin,
    IMembershipAdmin membershipAdmin) : IProfileService
{
    /// <inheritdoc/>
    public virtual async Task GetProfileDataAsync(ProfileDataRequestContext context, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var activity = Tracing.ServiceActivitySource.StartActivity("UserManagementProfileService.GetProfileData");

        context.LogProfileRequest(logger);

        var sub = context.Subject.GetSubjectId();
        if (sub == null)
        {
            logger.NoSubClaimPresent(LogLevel.Information);
            return;
        }

        if (!UserSubjectId.TryCreate(sub, out var subjectId))
        {
            logger.SubjectIdNotValid(LogLevel.Warning, sub);
            return;
        }

        await GetProfileDataAsync(context, subjectId, ct);
        context.LogIssuedClaims(logger);
    }

    /// <summary>
    /// Gets profile data for the given subject ID.
    /// </summary>
    protected virtual async Task GetProfileDataAsync(ProfileDataRequestContext context, UserSubjectId subjectId, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(subjectId);

        var profile = await TryFindUserProfileAsync(subjectId, ct);
        if (profile == null)
        {
            logger.NoUserProfileFound(LogLevel.Debug, subjectId);
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
            var code = attribute.Code.ToString();
            var untypedValue = attribute.UntypedValue;

            switch (untypedValue)
            {
                case IReadOnlyDictionary<string, object>:
                    logger.ComplexAttributeTypeNotSupported(LogLevel.Warning, code);
                    continue;

                case IReadOnlyList<object> list:
                    if (list.Any(e => e is IReadOnlyDictionary<string, object>))
                    {
                        logger.ComplexAttributeTypeNotSupported(LogLevel.Warning, code);
                        continue;
                    }

                    foreach (var element in list)
                    {
                        var (elemValue, elemType) = FormatScalarValue(element);
                        claims.Add(new Claim(code, elemValue, elemType));
                    }
                    continue;

                default:
                    var (scalarValue, scalarType) = FormatScalarValue(untypedValue);
                    claims.Add(new Claim(code, scalarValue, scalarType));
                    break;
            }
        }

        claims.AddRange(await GetRoleClaimsAsync(context, profile.SubjectId, ct));

        context.AddRequestedClaims(claims);
    }

    private static (string Value, string ValueType) FormatScalarValue(object? value) =>
        value switch
        {
            null => (string.Empty, ClaimValueTypes.String),
            bool b => (b ? "true" : "false", ClaimValueTypes.Boolean),
            int i => (i.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer32),
            decimal d => (d.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Double),
            DateTimeOffset dto => (dto.ToString("O", CultureInfo.InvariantCulture), ClaimValueTypes.DateTime),
            DateOnly date => (date.ToString("O", CultureInfo.InvariantCulture), ClaimValueTypes.Date),
            _ => (value.ToString() ?? string.Empty, ClaimValueTypes.String)
        };

    /// <inheritdoc/>
    public virtual async Task IsActiveAsync(IsActiveContext context, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var activity = Tracing.ServiceActivitySource.StartActivity("UserManagementProfileService.IsActive");

        logger.IsActiveCalled(LogLevel.Debug, context.Caller);

        var sub = context.Subject.GetSubjectId();
        if (sub == null)
        {
            context.IsActive = false;
            return;
        }

        if (!UserSubjectId.TryCreate(sub, out var subjectId))
        {
            logger.SubjectIdNotValidInactive(LogLevel.Warning, sub);
            context.IsActive = false;
            return;
        }

        var authenticators = await TryFindUserAuthenticatorsAsync(subjectId, ct);
        if (authenticators == null)
        {
            logger.UserHasNoAuthenticator(LogLevel.Information, subjectId);
            context.IsActive = false;
        }
        else
        {
            context.IsActive = await IsUserActiveAsync(subjectId, authenticators, ct);
        }
    }

    /// <summary>
    /// Determines whether the user is active based on their authenticators.
    /// </summary>
    /// <param name="subjectId">The user's subject ID.</param>
    /// <param name="authenticators">The user's authenticators.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Default implementation always returns true.</returns>
    protected virtual Task<bool> IsUserActiveAsync(UserSubjectId subjectId, UserAuthenticators authenticators, Ct ct) => Task.FromResult(true);

    /// <summary>
    /// Loads the user profile by subject ID.
    /// </summary>
    protected virtual Task<UserProfile?> TryFindUserProfileAsync(UserSubjectId subjectId, Ct ct) =>
        profileAdmin.TryGetAsync(subjectId, ct);

    /// <summary>
    /// Loads the user authenticators for a given subject id. This is used to determine if the user is active.
    /// </summary>
    protected virtual Task<UserAuthenticators?> TryFindUserAuthenticatorsAsync(UserSubjectId subjectId, Ct ct) =>
        authenticatorsAdmin.TryGetAsync(subjectId, ct);

    /// <summary>
    /// Gets role claims for the specified user.
    /// </summary>
    protected virtual async Task<IEnumerable<Claim>> GetRoleClaimsAsync(ProfileDataRequestContext context, UserSubjectId subjectId, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.RequestedClaimTypes.Contains(JwtClaimTypes.Role))
        {
            return [];
        }

        var directTask = GetAllRolesAsync((range, token) => membershipAdmin.GetDirectRolesAsync(subjectId, range, token), ct);
        var transitiveTask = GetAllRolesAsync((range, token) => membershipAdmin.GetTransitiveRolesAsync(subjectId, range, token), ct);

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
        var range = DataRange.FromPage(1);
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
