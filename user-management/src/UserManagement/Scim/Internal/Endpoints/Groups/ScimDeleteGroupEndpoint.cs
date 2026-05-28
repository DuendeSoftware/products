// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Licensing;
using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Groups;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ScimDeleteGroupEndpoint(ScimGroupCommandProcessor processor, UserManagementLicenseValidator licenseValidator)
{
    internal async Task<IResult> HandleAsync(
        string id,
        HttpContext context,
        Ct ct)
    {
        licenseValidator.ValidateInboundScim();
        var result = await processor.DeleteAsync(id, context.Request.Headers.IfMatch.FirstOrDefault(), ct);
        return ScimOperationResultHttpMapper.ToHttpResult(result, context.Response);
    }
}
