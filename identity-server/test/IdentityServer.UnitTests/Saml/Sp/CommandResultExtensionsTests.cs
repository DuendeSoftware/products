// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Internal.Saml.Sp.Commands;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace UnitTests.Saml.Sp;

public sealed class CommandResultExtensionsTests
{
    [Fact]
    public async Task Apply_replaces_existing_response_header_instead_of_appending()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Headers["Content-Security-Policy"] = "default-src 'self'";

        var commandResult = new CommandResult();
        commandResult.Headers["Content-Security-Policy"] = "script-src 'sha256-abc'";

        await commandResult.Apply(
            httpContext,
            dataProtector: new EphemeralDataProtectionProvider().CreateProtector("test"),
            cookieManager: new ChunkingCookieManager(),
            signInScheme: "scheme",
            signOutScheme: "scheme",
            emitSameSiteNone: false);

        httpContext.Response.Headers["Content-Security-Policy"].Count.ShouldBe(1);
        httpContext.Response.Headers["Content-Security-Policy"].ShouldBe(["script-src 'sha256-abc'"]);
    }
}
