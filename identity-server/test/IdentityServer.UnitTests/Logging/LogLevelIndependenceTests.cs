// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UnitTests.Common;
using UnitTests.Endpoints.Authorize;
using UnitTests.Endpoints.EndSession;

namespace UnitTests.Logging;

/// <summary>
/// A principal that is authenticated but lacks a sub claim causes GetSubjectId() to throw
/// InvalidOperationException. These tests assert that this request-processing failure is surfaced
/// consistently whether or not the relevant log level is enabled, since the request cannot be validated
/// without a subject id regardless of what gets logged about it.
/// </summary>
public class LogLevelIndependenceTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    /// <summary>
    /// An ILogger whose IsEnabled result for a single configured level is controllable, so tests can
    /// exercise both the enabled and disabled logging paths through the same production code.
    /// </summary>
    private sealed class LevelControlledLogger<T>(LogLevel controlledLevel, bool isEnabled) : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != controlledLevel || isEnabled;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                formatter(state, exception);
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private static ClaimsPrincipal PrincipalWithoutSubjectClaim() =>
        new(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Name, "bob"),
        ], "test"));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task authorize_endpoint_should_throw_when_user_lacks_sub_claim_regardless_of_log_level(bool debugEnabled)
    {
        var logger = new LevelControlledLogger<AuthorizeEndpointBaseTests.TestAuthorizeEndpoint>(LogLevel.Debug, debugEnabled);
        var user = PrincipalWithoutSubjectClaim();

        var validatedAuthorizeRequest = new ValidatedAuthorizeRequest
        {
            RedirectUri = "http://client/callback",
            State = "123",
            ResponseMode = "fragment",
            ClientId = "client",
            Client = new Client { ClientId = "client", ClientName = "Test Client" },
            Raw = [],
            Subject = new IdentityServerUser("bob").CreatePrincipal(),
        };

        var stubValidator = new StubAuthorizeRequestValidator
        {
            Result = new AuthorizeRequestValidationResult(validatedAuthorizeRequest)
            {
                IsError = true,
                Error = "login_required",
            },
        };

        var subject = new AuthorizeEndpointBaseTests.TestAuthorizeEndpoint(
            new TestEventService(),
            logger,
            new IdentityServerOptions(),
            stubValidator,
            new StubAuthorizeInteractionResponseGenerator(),
            new StubAuthorizeResponseGenerator(),
            new MockUserSession(),
            new MockConsentMessageStore());

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => subject.ProcessAuthorizeRequestAsync(new NameValueCollection(), user, _ct));

        exception.Message.ShouldBe("sub claim is missing");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task end_session_endpoint_should_throw_when_user_lacks_sub_claim_regardless_of_log_level(bool debugEnabled)
    {
        var logger = new LevelControlledLogger<EndSessionEndpoint>(LogLevel.Debug, debugEnabled);
        var userSession = new MockUserSession { User = PrincipalWithoutSubjectClaim() };
        var validator = new StubEndSessionRequestValidator();
        var subject = new EndSessionEndpoint(validator, userSession, logger);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => subject.ProcessAsync(context));

        exception.Message.ShouldBe("sub claim is missing");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void log_profile_request_should_throw_when_subject_lacks_sub_claim_regardless_of_log_level(bool debugEnabled)
    {
        var logger = new LevelControlledLogger<LogLevelIndependenceTests>(LogLevel.Debug, debugEnabled);
        var context = new ProfileDataRequestContext(
            PrincipalWithoutSubjectClaim(),
            new Client { ClientId = "client" },
            IdentityServerConstants.ProfileDataCallers.UserInfoEndpoint,
            ["name"]);

        var exception = Should.Throw<InvalidOperationException>(() => context.LogProfileRequest(logger));

        exception.Message.ShouldBe("sub claim is missing");
    }
}
