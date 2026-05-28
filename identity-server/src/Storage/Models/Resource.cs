// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Diagnostics;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models the common data of API and identity resources.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public abstract class Resource
{
    private string DebuggerDisplay => Name ?? $"{{{typeof(Resource)}}}";

    /// <summary>
    /// Gets or sets a value indicating whether this resource is enabled and can be requested. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the unique name of the resource. For API scopes, this is the value clients use in the scope parameter.
    /// For API resources, this value is added to the audience of the outgoing access token.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Gets or sets the display name of the resource. This value can be used on the consent screen.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description of the resource. This value can be used on the consent screen.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this scope is shown in the discovery document. Defaults to <c>true</c>.
    /// </summary>
    public bool ShowInDiscoveryDocument { get; set; } = true;

    /// <summary>
    /// Gets or sets the collection of associated user claim types that should be included when this resource is requested.
    /// </summary>
    public ICollection<string> UserClaims { get; set; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets the custom properties for the resource.
    /// </summary>
    /// <value>
    /// The properties.
    /// </value>
    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}
