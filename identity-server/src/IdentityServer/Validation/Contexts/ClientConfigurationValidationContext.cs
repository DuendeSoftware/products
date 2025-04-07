// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Context for client configuration validation.
/// </summary>
public class ClientConfigurationValidationContext
{
    /// <summary>
    /// Gets or sets the client.
    /// </summary>
    /// <value>
    /// The client.
    /// </value>
    public Client Client { get; }

    /// <summary>
    /// Returns true if client configuration is valid.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
    /// </value>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>
    /// The error message.
    /// </value>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientConfigurationValidationContext"/> class.
    /// </summary>
    /// <param name="client">The client.</param>
    public ClientConfigurationValidationContext(Client client) => Client = client;

    /// <summary>
    /// Sets a validation error.
    /// </summary>
    /// <param name="message">The message.</param>
    public void SetError(string message)
    {
        IsValid = false;
        ErrorMessage = message;
    }
}
