// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Blazor.Client.Internals;

/// <summary>
/// This class wraps our usage of the PersistentComponentState, mostly to facilitate testing.
/// </summary>
/// <param name="state"></param>
/// <param name="logger"></param>
internal class PersistentUserService(PersistentComponentState state, ILogger<PersistentUserService> logger) : IPersistentUserService
{
    /// <inheritdoc />
    public ClaimsPrincipal GetPersistedUser()
    {
        if (!state.TryTakeFromJson<ClaimsPrincipalRecord>(nameof(ClaimsPrincipalRecord), out var lite) || lite is null)
        {
            logger.LogDebug("Failed to load persisted user.");
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        logger.LogDebug("Persisted user loaded.");

        return lite.ToClaimsPrincipal();
    }
}