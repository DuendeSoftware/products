// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Represents a secret extracted from the HttpContext
/// </summary>
public class ParsedSecret
{
    /// <summary>
    /// Gets or sets the identifier associated with this secret
    /// </summary>
    /// <value>
    /// The identifier.
    /// </value>
    public string Id { get; set; } = default!;

    /// <summary>
    /// Gets or sets the credential to verify the secret
    /// </summary>
    /// <value>
    /// The credential.
    /// </value>
    public object? Credential { get; set; }

    /// <summary>
    /// Gets or sets the type of the secret
    /// </summary>
    /// <value>
    /// The type.
    /// </value>
    public string Type { get; set; } = default!;

    /// <summary>
    /// Gets or sets additional properties.
    /// </summary>
    /// <value>
    /// The properties.
    /// </value>
    public Dictionary<string, string> Properties { get; set; } = [];
}
