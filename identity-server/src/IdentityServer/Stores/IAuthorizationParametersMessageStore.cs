// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Interface for authorization request messages that are sent from the authorization endpoint to the login and consent UI.
/// </summary>
public interface IAuthorizationParametersMessageStore
{
    /// <summary>
    /// Writes the authorization parameters.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The identifier for the stored message.</returns>
    Task<string> WriteAsync(Message<IDictionary<string, string[]>> message);

    /// <summary>
    /// Reads the authorization parameters.
    /// </summary>
    /// <param name="id">The identifier.</param>
    Task<Message<IDictionary<string, string[]>>> ReadAsync(string id);

    /// <summary>
    /// Deletes the authorization parameters.
    /// </summary>
    /// <param name="id">The identifier.</param>
    Task DeleteAsync(string id);
}
