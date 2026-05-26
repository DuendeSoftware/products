// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Scim;

/// <summary>Options for configuring SCIM endpoint routes.</summary>
public sealed class ScimEndpointOptions
{
    /// <summary>
    /// The base route prefix for all SCIM user endpoints.
    /// Defaults to "/scim/Users".
    /// </summary>
    public string Route { get; set; } = "/scim/Users";

    /// <summary>
    /// The base route prefix for all SCIM group endpoints.
    /// Defaults to "/scim/Groups".
    /// </summary>
    public string GroupsRoute { get; set; } = "/scim/Groups";

    /// <summary>
    /// The base route prefix for SCIM metadata/discovery endpoints
    /// (ServiceProviderConfig, ResourceTypes, Schemas).
    /// Defaults to "/scim".
    /// </summary>
    public string MetadataRoute { get; set; } = "/scim";

    /// <summary>
    /// The route for the SCIM Bulk endpoint.
    /// Defaults to "/scim/Bulk".
    /// </summary>
    public string BulkRoute { get; set; } = "/scim/Bulk";
}
