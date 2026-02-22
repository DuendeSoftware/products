// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Otel;
using Duende.Bff.SessionManagement.SessionStore;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.SessionManagement.Revocation;

/// <summary>
/// Nop implementation of the user session store
/// </summary>
internal class NopSessionRevocationService(ILogger<NopSessionRevocationService> logger) : ISessionRevocationService
{
    /// <inheritdoc />
    public Task RevokeSessionsAsync(UserSessionsFilter filter, Ct ct = default)
    {
        logger.NopSessionRevocation(LogLevel.Debug, filter.SubjectId, filter.SessionId);
        return Task.CompletedTask;
    }
}
