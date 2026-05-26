// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Scim.Internal;

internal sealed record ScimOperationResult
{
    public required int StatusCode { get; init; }
    public object? Value { get; init; }
    public string? Location { get; init; }
    public string? ETag { get; init; }
    public string? ScimType { get; init; }
    public string? Detail { get; init; }
    public string? ResourceId { get; init; }

    public bool IsError => StatusCode >= 400;

    internal static ScimOperationResult Ok(object value) =>
        new()
        {
            StatusCode = 200,
            Value = value
        };

    internal static ScimOperationResult Ok(object value, string? eTag) =>
        new()
        {
            StatusCode = 200,
            Value = value,
            ETag = eTag
        };

    internal static ScimOperationResult Ok(object value, string? eTag, string? resourceId) =>
        new()
        {
            StatusCode = 200,
            Value = value,
            ETag = eTag,
            ResourceId = resourceId
        };

    internal static ScimOperationResult Created(object value, string location, string eTag) =>
        new()
        {
            StatusCode = 201,
            Value = value,
            Location = location,
            ETag = eTag
        };

    internal static ScimOperationResult Created(object value, string location, string eTag, string? resourceId) =>
        new()
        {
            StatusCode = 201,
            Value = value,
            Location = location,
            ETag = eTag,
            ResourceId = resourceId
        };

    internal static ScimOperationResult NoContent() =>
        new()
        {
            StatusCode = 204
        };

    internal static ScimOperationResult Error(int statusCode, string? scimType, string? detail) =>
        new()
        {
            StatusCode = statusCode,
            ScimType = scimType,
            Detail = detail
        };

    internal static ScimOperationResult Error(int statusCode, string detail) =>
        Error(statusCode, null, detail);
}
