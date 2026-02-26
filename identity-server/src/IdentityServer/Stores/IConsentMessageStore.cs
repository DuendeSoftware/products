// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Interface for consent messages that are sent from the consent UI to the authorization endpoint.
/// </summary>
public interface IConsentMessageStore
{
    /// <summary>
    /// Writes the consent response message.
    /// </summary>
    /// <param name="id">The id for the message.</param>
    /// <param name="message">The message.</param>
    /// <param name="ct">The cancellation token.</param>
    Task WriteAsync(string id, Message<ConsentResponse> message, Ct ct);

    /// <summary>
    /// Reads the consent response message.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<Message<ConsentResponse>> ReadAsync(string id, Ct ct);

    /// <summary>
    /// Deletes the consent response message.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task DeleteAsync(string id, Ct ct);
}
