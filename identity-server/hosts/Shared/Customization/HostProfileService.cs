// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;

namespace Duende.IdentityServer.Hosts.Shared.Customization;

public class HostProfileService(TestUserStore users, ILogger<TestUserProfileService> logger) : TestUserProfileService(users, logger)
{
    public override async Task GetProfileDataAsync(ProfileDataRequestContext context, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        await base.GetProfileDataAsync(context, ct);

        var transaction = context.RequestedResources?.ParsedScopes.FirstOrDefault(x => x.ParsedName == "transaction");
        if (transaction?.ParsedParameter != null)
        {
            context.IssuedClaims.Add(new Claim("transaction_id", transaction.ParsedParameter));
        }
    }
}
