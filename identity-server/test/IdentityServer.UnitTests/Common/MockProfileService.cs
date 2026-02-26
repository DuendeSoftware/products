// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class MockProfileService : IProfileService
{
    public ICollection<Claim> ProfileClaims { get; set; } = new HashSet<Claim>();
    public bool IsActive { get; set; } = true;

    public bool GetProfileWasCalled => ProfileContext != null;
    public ProfileDataRequestContext ProfileContext { get; set; }

    public bool IsActiveWasCalled => ActiveContext != null;
    public IsActiveContext ActiveContext { get; set; }

    public Task GetProfileDataAsync(ProfileDataRequestContext context, Ct _)
    {
        ProfileContext = context;
        context.IssuedClaims = ProfileClaims.ToList();
        return Task.CompletedTask;
    }

    public Task IsActiveAsync(IsActiveContext context, Ct _)
    {
        ActiveContext = context;
        context.IsActive = IsActive;
        return Task.CompletedTask;
    }
}
