// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Groups;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ScimPatchGroupEndpoint(ScimGroupCommandProcessor processor)
{
    internal async Task<IResult> HandleAsync(
        string id,
        ScimPatchRequest? body,
        HttpContext context,
        Ct ct)
    {
        var result = await processor.PatchAsync(id, body, context.Request.Headers.IfMatch.FirstOrDefault(), ct);
        return ScimOperationResultHttpMapper.ToHttpResult(result, context.Response);
    }
}
