// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Admin.ApiResources;

/// <summary>
/// Represents an API resource configuration for admin CRUD operations.
/// Mutable class — callers can <c>Get</c>, modify properties, and pass back to <c>UpdateAsync</c>.
/// </summary>
public class ApiResourceConfiguration
{
    /// <summary>
    /// The unique name of the API resource. Required.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Whether the API resource is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// A display-friendly name for the API resource.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// A description of the API resource.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this resource is shown in the discovery document. Defaults to <see langword="true"/>.
    /// </summary>
    public bool ShowInDiscoveryDocument { get; set; } = true;

    /// <summary>
    /// Whether this API resource requires the resource indicator to request it.
    /// </summary>
    public bool RequireResourceIndicator { get; set; }

    /// <summary>
    /// The collection of user claim types included when this resource is requested.
    /// </summary>
    public List<string>? UserClaims { get; set; }

    /// <summary>
    /// The collection of API scope names that this API resource exposes.
    /// </summary>
    public List<string>? Scopes { get; set; }

    /// <summary>
    /// The collection of allowed signing algorithms for access tokens issued to this resource.
    /// </summary>
    public List<string>? AllowedAccessTokenSigningAlgorithms { get; set; }

    /// <summary>
    /// API secrets — metadata only. The secret value is never exposed.
    /// Metadata (description, expiration, type) can be modified and saved via <c>UpdateAsync</c>.
    /// To add a new secret, use <c>CreateSecretAsync</c> (accepts plaintext, hashes before storage).
    /// To change a secret value, delete the existing secret and create a new one.
    /// </summary>
    public List<ApiResourceSecretConfiguration>? ApiSecrets { get; set; }

    /// <summary>
    /// Schema-validated extended properties for this API resource.
    /// Values are validated against the registered API resource schema when creating or updating.
    /// </summary>
    public AttributeValueCollection ExtendedProperties { get; init; } = new();

    /// <summary>
    /// Data version for optimistic concurrency. <see langword="null"/> for new resources.
    /// </summary>
    public DataVersion? Version { get; set; }
}
