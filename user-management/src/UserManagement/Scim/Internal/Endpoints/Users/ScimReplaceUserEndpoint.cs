// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Users;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ScimReplaceUserEndpoint(ScimUserCommandProcessor processor)
{
    internal async Task<IResult> HandleAsync(
        string id,
        ScimUserRequest? body,
        HttpContext context,
        Ct ct)
    {
        var result = await processor.ReplaceAsync(id, body, context.Request.Headers.IfMatch.FirstOrDefault(), ct);
        return ScimOperationResultHttpMapper.ToHttpResult(result, context.Response);
    }
}
