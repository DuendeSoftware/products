// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.SessionStore;

internal class DefaultUserSessionPartitionKeyBuilder(
    IOptions<DataProtectionOptions> options,
    SelectedFrontend selectedFrontend)
    : IUserSessionPartitionKeyBuilder
{
    public string? BuildPartitionKey()
    {
        var appName = options.Value.ApplicationDiscriminator;
        if (selectedFrontend.TryGet(out var frontend))
        {
            return appName == null
                ? frontend.Name.ToString()
                : appName + "|" + frontend.Name.ToString();
        }

        // In v3, a null value for an appname was used. This can cause issues, because
        // a null value is ignored from indexes, which causes unique constraints to be ignored.

        return appName ?? "";
    }
}
