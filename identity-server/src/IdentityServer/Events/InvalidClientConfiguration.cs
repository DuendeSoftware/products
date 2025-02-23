// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for unhandled exceptions
/// </summary>
/// <seealso cref="Event" />
public class InvalidClientConfigurationEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidClientConfigurationEvent" /> class.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="errorMessage">The error message.</param>
    public InvalidClientConfigurationEvent(Client client, string errorMessage)
        : base(EventCategories.Error,
            "Invalid Client Configuration",
            EventTypes.Error, 
            EventIds.InvalidClientConfiguration,
            errorMessage)
    {
        ClientId = client.ClientId;
        ClientName = client.ClientName ?? "unknown name";
    }

    /// <summary>
    /// Gets or sets the client ID.
    /// </summary>
    /// <value>
    /// The details.
    /// </value>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the name of the client.
    /// </summary>
    /// <value>
    /// The name of the client.
    /// </value>
    public string ClientName { get; set; }
}