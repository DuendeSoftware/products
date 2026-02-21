// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Interface to model loading the keys.
/// </summary>
public interface IKeyManager
{
    /// <summary>
    /// Returns the current signing keys.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<IEnumerable<KeyContainer>> GetCurrentKeysAsync(CT ct);

    /// <summary>
    /// Returns all the validation keys.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<IEnumerable<KeyContainer>> GetAllKeysAsync(CT ct);
}
