// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Bff;

/// <summary>
/// Nop implementation of the user session store
/// </summary>
[Obsolete(Constants.ObsoleteMessages.ImplementationWillBeMadeInternal)]
public class NopSessionRevocationService(ILogger<NopSessionRevocationService> logger) : ISessionRevocationService
{
    /// <inheritdoc />
    public Task RevokeSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Nop implementation of session revocation for sub: {sub}, and sid: {sid}. Implement ISessionRevocationService to provide your own implementation.", filter.SubjectId, filter.SessionId);
        return Task.CompletedTask;
    }
}
