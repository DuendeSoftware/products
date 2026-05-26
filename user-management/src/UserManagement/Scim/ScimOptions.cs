// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Scim;

/// <summary>Options for configuring SCIM capabilities.</summary>
public sealed class ScimOptions
{
    /// <summary>
    /// Whether SCIM User resource endpoints are enabled.
    /// When <c>null</c> (default), auto-detected from DI by checking
    /// whether <c>IUserProfileAdmin</c> is registered.
    /// </summary>
    public bool? EnableUsers { get; set; }

    /// <summary>
    /// Whether SCIM Group resource endpoints are enabled.
    /// When <c>null</c> (default), auto-detected from DI by checking
    /// whether <c>IGroupAdmin</c> is registered.
    /// </summary>
    public bool? EnableGroups { get; set; }

    /// <summary>
    /// Whether the SCIM changePassword capability is supported.
    /// When <c>null</c> (default), auto-detected from DI by checking
    /// whether <c>IPasswordAuth</c> is registered.
    /// </summary>
    public bool? ChangePassword { get; set; }

    /// <summary>
    /// Maximum number of resources returned in a single list response.
    /// Defaults to 200.
    /// </summary>
    public int MaxResults { get; set; } = 200;

    /// <summary>
    /// Maximum number of group members included in a single group response.
    /// When a group has more members than this limit, the members array is truncated
    /// and a warning is logged. Callers can use <c>excludedAttributes=members</c> to
    /// skip member loading entirely for large groups.
    /// Defaults to 100.
    /// </summary>
    public int MaxGroupMembersInResponse { get; set; } = 100;

    /// <summary>
    /// Maximum number of operations allowed in a single bulk request.
    /// If exceeded, the service provider returns HTTP 413.
    /// Defaults to 100.
    /// </summary>
    public int MaxBulkOperations { get; set; } = 100;

    /// <summary>
    /// Maximum payload size (in bytes) allowed for a single bulk request.
    /// If the <c>Content-Length</c> header exceeds this value, the service provider returns HTTP 413.
    /// Defaults to 1,048,576 (1 MB).
    /// </summary>
    public int MaxBulkPayloadSize { get; set; } = 1_048_576;
}
