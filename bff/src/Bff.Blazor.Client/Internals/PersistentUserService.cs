// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Duende.Bff.Internal;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Blazor.Client.Internals;

/// <summary>
/// This class wraps our usage of the PersistentComponentState, mostly to facilitate testing.
/// </summary>
internal class PersistentUserService
{
    private readonly PersistentComponentState _state;
    private readonly ILogger<PersistentUserService> _logger;

    /// <summary>
    /// This class wraps our usage of the PersistentComponentState, mostly to facilitate testing.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="logger"></param>
    public PersistentUserService(PersistentComponentState state, ILogger<PersistentUserService> logger)
    {
        _state = state;
        _logger = logger;
    }

    /// <summary>
    /// Parameterless constructor for testing only.
    /// </summary>
    public PersistentUserService()
    {
        _state = null!;
        _logger = null!;
    }

    /// <summary>
    /// Attempts to retrieve a ClaimsPrincipal from PersistentComponentState, and indicates success or failure with the
    /// return value.
    /// </summary>
    /// <returns>True if a <see cref="ClaimsPrincipal"/> could be retrieved from Persistent Component State, and false
    /// otherwise. Even if the ClaimsPrincipal is not authenticated, this method will still return true. This method
    /// only returns false when there is a failure to read the ClaimsPrincipal entirely, perhaps because no
    /// authentication state was persisted.</returns>
    public virtual bool GetPersistedUser([NotNullWhen(true)] out ClaimsPrincipal? user)
    {
        if (!_state.TryTakeFromJson<ClaimsPrincipalRecord>(nameof(ClaimsPrincipalRecord), out var lite) || lite is null)
        {
            _logger.FailedToLoadPersistedUser();
            user = null;
            return false;
        }

        _logger.PersistedUserLoaded();

        user = lite.ToClaimsPrincipal();
        return true;
    }
}
