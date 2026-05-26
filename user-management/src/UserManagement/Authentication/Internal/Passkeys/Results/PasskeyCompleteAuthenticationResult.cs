// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Authentication.Internal.Passkeys.Results;

internal sealed class PasskeyCompleteAuthenticationResult(bool userVerified, bool backedUp) : IResult
{
    public bool UserVerified => userVerified;
    public bool BackedUp => backedUp;

    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.SetNoCache();
        var dto = new ResultDto { userVerified = UserVerified, backedUp = BackedUp };
        await context.Response.WriteJsonAsync(dto);
    }

    private sealed record ResultDto
    {
        public bool userVerified { get; init; }
        public bool backedUp { get; init; }
    }
}
