// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Scim.Internal.Endpoints.Bulk;

/// <summary>
/// Parses a SCIM bulk operation path and method to determine the target
/// resource type and ID.
/// </summary>
internal static class BulkOperationRouter
{
    internal static BulkRouteResult Parse(string method, string path)
    {
        // Normalize path — strip leading slash for consistent parsing
        var normalized = path.TrimStart('/');
        var segments = normalized.Split('/', 2);

        var resourceSegment = segments[0];
        var idSegment = segments.Length > 1 ? segments[1] : null;

        // Determine resource type from first segment
        string resourceType;
        if (resourceSegment.Equals(ScimConstants.ResourceTypes.Users, StringComparison.OrdinalIgnoreCase))
        {
            resourceType = ScimConstants.ResourceTypes.User;
        }
        else if (resourceSegment.Equals(ScimConstants.ResourceTypes.Groups, StringComparison.OrdinalIgnoreCase))
        {
            resourceType = ScimConstants.ResourceTypes.Group;
        }
        else
        {
            return BulkRouteResult.Invalid(
                $"Unknown resource type '{resourceSegment}'. Supported types: Users, Groups.");
        }

        if (method.Equals(HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase))
        {
            return ParsePost(resourceType, idSegment);
        }

        if (method.Equals(HttpMethod.Put.Method, StringComparison.OrdinalIgnoreCase) ||
            method.Equals(HttpMethod.Patch.Method, StringComparison.OrdinalIgnoreCase) ||
            method.Equals(HttpMethod.Delete.Method, StringComparison.OrdinalIgnoreCase))
        {
            return ParseWithId(method, resourceType, idSegment);
        }

        return BulkRouteResult.Invalid(
            $"Unsupported method '{method}'. Bulk operations support {HttpMethod.Post.Method}, {HttpMethod.Put.Method}, {HttpMethod.Patch.Method}, and {HttpMethod.Delete.Method}.");
    }

    private static BulkRouteResult ParsePost(string resourceType, string? idSegment)
    {
        if (idSegment is not null)
        {
            return BulkRouteResult.Invalid(
                $"POST operations must target a resource type endpoint (e.g., /Users), not a specific resource.");
        }

        return BulkRouteResult.Valid(resourceType, null);
    }

    private static BulkRouteResult ParseWithId(string method, string resourceType, string? idSegment)
    {
        if (string.IsNullOrEmpty(idSegment))
        {
            return BulkRouteResult.Invalid(
                $"{method} operations must target a specific resource (e.g., /Users/{{id}}).");
        }

        return BulkRouteResult.Valid(resourceType, idSegment);
    }
}

internal readonly record struct BulkRouteResult
{
    public BulkRouteResult() => throw new InvalidOperationException();

    private BulkRouteResult(bool isValid, string? resourceType, string? resourceId, string? errorDetail)
    {
        IsValid = isValid;
        ResourceType = resourceType;
        ResourceId = resourceId;
        ErrorDetail = errorDetail;
    }

    public bool IsValid { get; }
    public string? ResourceType { get; }

    /// <summary>
    /// The resource ID. For POST operations this is null.
    /// May contain a <c>bulkId:</c>-prefixed reference before resolution.
    /// </summary>
    public string? ResourceId { get; }

    public string? ErrorDetail { get; }

    internal static BulkRouteResult Valid(string resourceType, string? resourceId) =>
        new(true, resourceType, resourceId, null);

    internal static BulkRouteResult Invalid(string errorDetail) =>
        new(false, null, null, errorDetail);
}
