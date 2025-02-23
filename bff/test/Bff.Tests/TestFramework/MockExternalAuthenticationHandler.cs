// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Tests.TestFramework;

public class MockExternalAuthenticationHandler : RemoteAuthenticationHandler<MockExternalAuthenticationOptions>,
    IAuthenticationSignOutHandler
{
    public MockExternalAuthenticationHandler(
        IOptionsMonitor<MockExternalAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    public bool ChallengeWasCalled { get; set; } = false;
    public AuthenticationProperties? ChallengeAuthenticationProperties { get; set; }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        ChallengeWasCalled = true;
        ChallengeAuthenticationProperties = properties;
        return Task.CompletedTask;
    }

    protected override Task<HandleRequestResult> HandleRemoteAuthenticateAsync()
    {
        var result = HandleRequestResult.NoResult();
        return Task.FromResult(result);
    }

    public bool SignOutWasCalled { get; set; }
    public AuthenticationProperties? SignOutAuthenticationProperties { get; set; }

    public Task SignOutAsync(AuthenticationProperties? properties)
    {
        SignOutWasCalled = true;
        SignOutAuthenticationProperties = properties;
        return Task.CompletedTask;
    }
}

public class MockExternalAuthenticationOptions : RemoteAuthenticationOptions
{
    public MockExternalAuthenticationOptions()
    {
        CallbackPath = "/external-callback";
    }
}