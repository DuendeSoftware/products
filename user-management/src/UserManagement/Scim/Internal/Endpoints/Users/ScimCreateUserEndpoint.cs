// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Licensing;
using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Users;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ScimCreateUserEndpoint(
    ScimUserCommandProcessor processor,
    UserManagementLicenseValidator licenseValidator)
{
    internal async Task<IResult> HandleAsync(
        ScimUserRequest? body,
        HttpContext context,
        Ct ct)
    {
        licenseValidator.ValidateInboundScim();
        var result = await processor.CreateAsync(body, ct);
        return ScimOperationResultHttpMapper.ToHttpResult(result, context.Response);
    }
}
