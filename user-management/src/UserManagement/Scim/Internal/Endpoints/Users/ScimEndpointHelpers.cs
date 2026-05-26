// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Querying;
using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Users;

/// <summary>
/// Shared helper methods for SCIM endpoint query-string parsing and precondition checks.
/// </summary>
internal static class ScimEndpointHelpers
{
    /// <summary>
    /// Evaluates the <c>If-Match</c> precondition header for optimistic concurrency.
    /// Returns an error <see cref="IResult"/> when the precondition fails, or <c>null</c> when
    /// the request should proceed (header absent, wildcard, or version matches).
    /// </summary>
    internal static IResult? CheckIfMatch(HttpContext context, int? currentVersion)
    {
        var ifMatch = context.Request.Headers.IfMatch.FirstOrDefault();
        return CheckIfMatch(ifMatch, currentVersion);
    }

    internal static IResult? CheckIfMatch(string? ifMatch, int? currentVersion)
    {
        if (ifMatch is null)
        {
            return null; // No header — proceed
        }

        if (!ScimETag.TryCreate(ifMatch, out var etag))
        {
            return ScimResults.Error(400, ScimConstants.ErrorTypes.InvalidSyntax, "Invalid ETag format.");
        }

        if (currentVersion is not null && !etag.Matches(currentVersion.Value))
        {
            return ScimResults.Error(412, detail: "Precondition failed: ETag mismatch.");
        }

        return null; // Passes
    }

    internal static ScimOperationResult? CheckIfMatchResult(string? ifMatch, int? currentVersion)
    {
        if (ifMatch is null)
        {
            return null;
        }

        if (!ScimETag.TryCreate(ifMatch, out var etag))
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax, "Invalid ETag format.");
        }

        if (currentVersion is not null && !etag.Matches(currentVersion.Value))
        {
            return ScimOperationResult.Error(412, detail: "Precondition failed: ETag mismatch.");
        }

        return null;
    }

    /// <summary>
    /// Evaluates the <c>If-None-Match</c> precondition header for conditional GETs.
    /// Returns a 304 <see cref="IResult"/> when the resource has not been modified,
    /// an error when the header is malformed, or <c>null</c> when the full response should be sent.
    /// </summary>
    internal static IResult? CheckIfNoneMatch(HttpContext context, int currentVersion)
    {
        var ifNoneMatch = context.Request.Headers.IfNoneMatch.FirstOrDefault();
        if (ifNoneMatch is null)
        {
            return null; // No header — proceed with full response
        }

        if (!ScimETag.TryCreate(ifNoneMatch, out var etag))
        {
            return ScimResults.Error(400, ScimConstants.ErrorTypes.InvalidSyntax, "Invalid ETag format.");
        }

        if (etag.Matches(currentVersion))
        {
            return ScimResults.NotModified();
        }

        return null; // Version differs — send full response
    }

    /// <summary>
    /// Parses a comma-separated attribute list (e.g. SCIM <c>attributes</c> or
    /// <c>excludedAttributes</c> query parameter) into a case-insensitive set.
    /// Returns null when the input is null or whitespace-only.
    /// </summary>
    internal static HashSet<string>? ParseAttributeSet(string? attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes))
        {
            return null;
        }

        return new HashSet<string>(
            attributes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses an array of attribute names into a case-insensitive set.
    /// Returns null when the array is null or empty.
    /// </summary>
    internal static HashSet<string>? ParseAttributeSet(string[]? attributes)
    {
        if (attributes is null || attributes.Length == 0)
        {
            return null;
        }

        return new HashSet<string>(attributes, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the SCIM <c>sortOrder</c> value into a <see cref="SortDirection"/>.
    /// Defaults to <see cref="SortDirection.Ascending"/> per RFC 7644 §3.4.2.3.
    /// </summary>
    internal static SortDirection ParseSortDirection(string? sortOrder) =>
        string.Equals(sortOrder, "descending", StringComparison.OrdinalIgnoreCase)
            ? SortDirection.Descending
            : SortDirection.Ascending;
}
