// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;

namespace Duende.Bff.Blazor.Client.Internals;

/// <summary>
/// A service for interacting with the user persisted in PersistentComponentState in blazor.
/// </summary>
public interface IPersistentUserService
{
    /// <summary>
    /// Retrieves a ClaimsPrincipal from PersistentComponentState. If there is no persisted user, returns an anonymous
    /// user.
    /// </summary>
    /// <returns></returns>
    ClaimsPrincipal GetPersistedUser();
}