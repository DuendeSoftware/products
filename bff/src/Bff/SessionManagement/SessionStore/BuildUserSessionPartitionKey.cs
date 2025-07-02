// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.SessionStore;

public delegate PartitionKey BuildUserSessionPartitionKey();

internal class UserSessionPartitionKeyBuilder(
    IOptions<DataProtectionOptions> options,
    CurrentFrontendAccessor currentFrontendAccessor)
{
    public PartitionKey BuildPartitionKey()
    {
        var applicationDiscriminator = options.Value.ApplicationDiscriminator?.Replace('|', ':');
        if (currentFrontendAccessor.TryGet(out var frontend))
        {
            return PartitionKey.Parse(applicationDiscriminator == null
                ? frontend.Name.ToString()
                : applicationDiscriminator + '|' + frontend.Name);
        }

        // In v3, a null value for an appname was used. This can cause issues, because
        // a null value is ignored from indexes, which causes unique constraints to be ignored.
        return PartitionKey.Parse(applicationDiscriminator ?? "");
    }
}
