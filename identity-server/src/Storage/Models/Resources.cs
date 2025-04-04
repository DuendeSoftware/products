// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models a collection of identity and API resources.
/// </summary>
public class Resources
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Resources"/> class.
    /// </summary>
    public Resources()
    {
        IdentityResources = new HashSet<IdentityResource>();
        ApiResources = new HashSet<ApiResource>();
        ApiScopes = new HashSet<ApiScope>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Resources"/> class.
    /// </summary>
    /// <param name="other">The other.</param>
    public Resources(Resources other)
        : this(other.IdentityResources, other.ApiResources, other.ApiScopes) => OfflineAccess = other.OfflineAccess;

    /// <summary>
    /// Initializes a new instance of the <see cref="Resources"/> class.
    /// </summary>
    /// <param name="identityResources">The identity resources.</param>
    /// <param name="apiResources">The API resources.</param>
    /// <param name="apiScopes">The API scopes.</param>
    public Resources(IEnumerable<IdentityResource>? identityResources, IEnumerable<ApiResource>? apiResources, IEnumerable<ApiScope>? apiScopes)
    {
        IdentityResources = identityResources?.ToHashSet() ?? new HashSet<IdentityResource>();
        ApiResources = apiResources?.ToHashSet() ?? new HashSet<ApiResource>();
        ApiScopes = apiScopes?.ToHashSet() ?? new HashSet<ApiScope>();
    }

    /// <summary>
    /// Gets or sets a value indicating whether [offline access].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [offline access]; otherwise, <c>false</c>.
    /// </value>
    public bool OfflineAccess { get; set; }

    /// <summary>
    /// Gets or sets the identity resources.
    /// </summary>
    public ICollection<IdentityResource> IdentityResources { get; set; }

    /// <summary>
    /// Gets or sets the API resources.
    /// </summary>
    public ICollection<ApiResource> ApiResources { get; set; }

    /// <summary>
    /// Gets or sets the API scopes.
    /// </summary>
    public ICollection<ApiScope> ApiScopes { get; set; }
}
