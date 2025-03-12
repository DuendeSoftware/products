// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Bff;

/// <summary>
/// Nop implementation of the user session store
/// </summary>
public class NopSessionRevocationService : ISessionRevocationService
{
    private readonly ILogger<NopSessionRevocationService> _logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="logger"></param>
    public NopSessionRevocationService(ILogger<NopSessionRevocationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task RevokeSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Nop implementation of session revocation for sub: {sub}, and sid: {sid}. Implement ISessionRevocationService to provide your own implementation.", filter.SubjectId, filter.SessionId);
        return Task.CompletedTask;
    }
}
