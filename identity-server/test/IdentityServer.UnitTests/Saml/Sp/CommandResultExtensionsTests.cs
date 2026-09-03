// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Internal.Saml.Sp.Commands;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using UnitTests.Common;

namespace UnitTests.Saml.Sp;

public sealed class CommandResultExtensionsTests
{
    [Fact]
    public async Task apply_should_overwrite_existing_response_header()
    {
        const string headerName = "Content-Security-Policy";
        const string expectedHeader = "script-src 'sha256-test'";
        var commandResult = new CommandResult();
        commandResult.Headers[headerName] = expectedHeader;
        var context = new DefaultHttpContext();
        context.Response.Headers[headerName] = "default-src 'self'";

        await commandResult.Apply(
            context,
            new StubDataProtectionProvider(),
            new ChunkingCookieManager(),
            signInScheme: "sign-in",
            signOutScheme: "sign-out",
            emitSameSiteNone: false);

        context.Response.Headers[headerName].Count.ShouldBe(1);
        context.Response.Headers[headerName].ToString().ShouldBe(expectedHeader);
    }
}
