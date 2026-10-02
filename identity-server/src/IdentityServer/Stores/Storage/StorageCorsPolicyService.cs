// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores.Storage.Clients;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class StorageCorsPolicyService(ClientRepository repository, ILogger<StorageCorsPolicyService> logger) : ICorsPolicyService
{
    /// <inheritdoc/>
    public async Task<bool> IsOriginAllowedAsync(string origin, Ct ct)
    {
        var isAllowed = await repository.HasClientWithCorsOriginAsync(origin, ct);

        if (isAllowed)
        {
            logger.ClientListCheckedAndOriginOriginIsAllowed(origin);
        }
        else
        {
            logger.ClientListCheckedAndOriginOriginIsNot(origin);
        }

        return isAllowed;
    }
}
