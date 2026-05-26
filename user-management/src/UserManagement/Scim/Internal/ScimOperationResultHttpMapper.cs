// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Scim.Internal;

internal static class ScimOperationResultHttpMapper
{
    internal static IResult ToHttpResult(ScimOperationResult result, HttpResponse response)
    {
        if (result.ETag is not null)
        {
            response.Headers.ETag = result.ETag;
        }

        return result.StatusCode switch
        {
            200 => ScimResults.Ok(result.Value!),
            201 => ScimResults.Created(result.Value!, result.Location!, response),
            204 => ScimResults.NoContent(),
            _ => ScimResults.Error(result.StatusCode, result.ScimType, result.Detail)
        };
    }
}
