// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Scim.Internal;

internal static class ScimResults
{
    internal static IResult Ok<T>(T value) =>
        Results.Json(value, statusCode: 200, contentType: ScimConstants.ScimContentType);

    internal static IResult Created<T>(T value, string location, HttpResponse response)
    {
        response.Headers.Location = location;
        return Results.Json(value, statusCode: 201, contentType: ScimConstants.ScimContentType);
    }

    internal static IResult Error(int statusCode, string? scimType, string? detail) =>
        Results.Json(
            new ScimErrorResponse
            {
                Status = statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ScimType = scimType,
                Detail = detail
            },
            statusCode: statusCode,
            contentType: ScimConstants.ScimContentType);

    internal static IResult Error(int statusCode, string detail) =>
        Error(statusCode, null, detail);

    internal static IResult NoContent() => Results.NoContent();

    internal static IResult NotModified() => Results.StatusCode(304);
}
