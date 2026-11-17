// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;

/// <summary>
/// A minimal <see cref="IProfileService" /> over User Management (D9, D20): issues a claim per profile attribute,
/// filtered to what the request asked for, the same way
/// <c>Duende.IdentityServer.UserManagement.UserManagementProfileService</c> does. Reimplemented here, rather than
/// referenced, because that package is built against a published Duende.IdentityServer, so its
/// <c>IIdentityServerBuilder</c> and <c>IProfileService</c> are different types from this repository's.
/// </summary>
internal sealed class UserManagementProfileService(IUserProfileAdmin profileAdmin) : IProfileService
{
    public async Task GetProfileDataAsync(ProfileDataRequestContext context, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sub = context.Subject.GetSubjectId();
        if (sub is null || !UserSubjectId.TryCreate(sub, out var subjectId))
        {
            return;
        }

        var profile = await profileAdmin.TryGetAsync(subjectId, ct);
        if (profile is null)
        {
            return;
        }

        var claims = profile.Attributes.Values.Select(attribute => new Claim(attribute.Code.ToString(), attribute.UntypedValue?.ToString() ?? ""));
        context.AddRequestedClaims(claims);
    }

    public Task IsActiveAsync(IsActiveContext context, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.IsActive = true;
        return Task.CompletedTask;
    }
}
