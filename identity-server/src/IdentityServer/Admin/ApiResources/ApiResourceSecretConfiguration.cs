// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin.ApiResources;

/// <summary>
/// Secret metadata for an API resource. The secret value is NEVER exposed.
/// Metadata can be modified and saved via <c>IApiResourceAdmin.UpdateAsync</c>.
/// </summary>
public class ApiResourceSecretConfiguration
{
    /// <summary>
    /// The unique storage identifier for this secret.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// An optional description for this secret.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The optional expiration date for this secret.
    /// </summary>
    public DateTime? Expiration { get; set; }

    /// <summary>
    /// The secret type (e.g., <c>"SharedSecret"</c>, <c>"X509Thumbprint"</c>).
    /// </summary>
    public required string Type { get; set; }
}
