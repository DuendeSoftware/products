// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Authentication.Internal.Passkeys.Results;

internal sealed class PasskeyCompleteRegistrationResult(string credentialId) : IResult
{
    public string CredentialId => credentialId;

    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.SetNoCache();
        var dto = new ResultDto { credentialId = CredentialId };
        await context.Response.WriteJsonAsync(dto);
    }

    internal sealed record ResultDto
    {
        public required string credentialId { get; init; }
    }
}
