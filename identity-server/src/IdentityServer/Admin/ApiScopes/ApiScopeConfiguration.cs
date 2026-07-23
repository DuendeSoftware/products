// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.ApiScopes;

/// <summary>
/// Represents an API scope configuration for admin CRUD operations.
/// Mutable class — callers can <c>Get</c>, modify properties, and pass back to <c>UpdateAsync</c>.
/// </summary>
public class ApiScopeConfiguration
{
    /// <summary>
    /// The unique name of the API scope. Required.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Whether the API scope is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// A display-friendly name for the API scope.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// A description of the API scope.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this scope is shown in the discovery document. Defaults to <see langword="true"/>.
    /// </summary>
    public bool ShowInDiscoveryDocument { get; set; } = true;

    /// <summary>
    /// Whether the user can de-select the scope on the consent screen. Defaults to <see langword="false"/>.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Whether the consent screen will emphasize this scope. Defaults to <see langword="false"/>.
    /// </summary>
    public bool Emphasize { get; set; }

    /// <summary>
    /// The collection of user claim types included when this scope is requested.
    /// </summary>
    public List<string>? UserClaims { get; set; }

    /// <summary>
    /// Extended typed properties for schema-validated extensibility.
    /// </summary>
    public AttributeValueCollection ExtendedProperties { get; init; } = new();

    /// <summary>
    /// Data version for optimistic concurrency. <see langword="null"/> for new scopes.
    /// </summary>
    public DataVersion? Version { get; set; }
}
