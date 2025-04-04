// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Encodings.Web;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.IntegrationTests.TestFramework
{
    public class MockExternalAuthenticationHandler : RemoteAuthenticationHandler<MockExternalAuthenticationOptions>, IAuthenticationSignOutHandler
    {
        public MockExternalAuthenticationHandler(
            IOptionsMonitor<MockExternalAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        public bool ChallengeWasCalled { get; set; }
        public AuthenticationProperties ChallengeAuthenticationProperties { get; set; }
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
        public AuthenticationProperties SignOutAuthenticationProperties { get; set; }
        public Task SignOutAsync(AuthenticationProperties properties)
        {
            SignOutWasCalled = true;
            SignOutAuthenticationProperties = properties;
            return Task.CompletedTask;
        }
    }

    public class MockExternalAuthenticationOptions : RemoteAuthenticationOptions
    {
        public MockExternalAuthenticationOptions() => CallbackPath = "/external-callback";
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MockExternalAuthenticationExtensions
    {
        public static AuthenticationBuilder AddMockExternalAuthentication(this AuthenticationBuilder builder,
            string authenticationScheme = "external",
            string displayName = "external",
            Action<MockExternalAuthenticationOptions> configureOptions = null) => builder.AddRemoteScheme<MockExternalAuthenticationOptions, MockExternalAuthenticationHandler>(authenticationScheme, displayName, configureOptions);
    }
}
