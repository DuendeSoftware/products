// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using UnitTests.Common;
using UnitTests.Endpoints.Authorize;
using UnitTests.Validation.Setup;

namespace UnitTests.Logging;

/// <summary>
/// Validation request details are logged as structured objects so that a structured logging provider
/// such as Serilog can destructure them into queryable properties. These tests wire the real validators
/// through a real Serilog pipeline (Serilog.Extensions.Logging's SerilogLoggerProvider inside a
/// Microsoft.Extensions.Logging LoggerFactory) and assert that the rendered message does not contain a
/// literal, unexpanded destructuring placeholder, and that the request details are present as a
/// structured value with a ClientId property.
/// </summary>
public class SerilogValidationLoggingTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private sealed class InMemorySink : Serilog.Core.ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static (ILoggerFactory Factory, InMemorySink Sink) CreatePipeline()
    {
        var sink = new InMemorySink();
        var serilogLogger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var provider = new Serilog.Extensions.Logging.SerilogLoggerProvider(serilogLogger);
        var factory = LoggerFactory.Create(b =>
        {
            b.SetMinimumLevel(LogLevel.Trace);
            b.AddProvider(provider);
        });
        return (factory, sink);
    }

    private static LogEvent FindEventContaining(IEnumerable<LogEvent> events, string substring, LogEventLevel level) =>
        events.Single(e => e.Level == level && e.RenderMessage().Contains(substring));

    private static bool HasStructureValueWithProperty(LogEvent evt, string propertyName) =>
        evt.Properties.Values.OfType<StructureValue>().Any(sv => sv.Properties.Any(p => p.Name == propertyName));

    private static void AssertDestructured(LogEvent evt)
    {
        evt.RenderMessage().ShouldNotContain("{@");
        HasStructureValueWithProperty(evt, "ClientId").ShouldBeTrue();
    }

    private static void SetLogger(TokenRequestValidator validator, ILogger<TokenRequestValidator> logger)
    {
        var field = typeof(TokenRequestValidator).GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(validator, logger);
    }

    private static async Task<(TokenRequestValidator Validator, NameValueCollection Parameters, ClientSecretValidationResult ClientResult)> CreateAuthorizationCodeRequestAsync(
        ILogger<TokenRequestValidator> logger,
        bool includeCodeVerifier,
        Ct ct)
    {
        var clients = Factory.CreateClientStore();
        var client = await clients.FindEnabledClientByIdAsync("codeclient", ct);
        var store = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            IsOpenId = true,
            RedirectUri = "https://server/cb",
            Subject = new IdentityServerUser("bob").CreatePrincipal(),
            RequestedScopes = ["openid"],
        };
        var handle = await store.StoreAuthorizationCodeAsync(code, ct);

        var validator = Factory.CreateTokenRequestValidator(authorizationCodeStore: store);
        SetLogger(validator, logger);

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode },
            { OidcConstants.TokenRequest.Code, handle },
            { OidcConstants.TokenRequest.RedirectUri, "https://server/cb" },
        };
        if (includeCodeVerifier)
        {
            parameters.Add(OidcConstants.TokenRequest.CodeVerifier, "x".Repeat(43));
        }

        return (validator, parameters, client.ToValidationResult());
    }

    [Fact]
    public async Task token_request_validation_details_should_be_destructured_by_serilog_when_grant_type_missing()
    {
        var (factory, sink) = CreatePipeline();
        using var _ = factory;
        var logger = factory.CreateLogger<TokenRequestValidator>();
        var validator = Factory.CreateTokenRequestValidator();
        SetLogger(validator, logger);
        var clients = Factory.CreateClientStore();
        var client = await clients.FindEnabledClientByIdAsync("codeclient", _ct);

        var result = await validator.ValidateRequestAsync(new NameValueCollection(), client.ToValidationResult(), _ct);

        result.Error.ShouldBe(OidcConstants.TokenErrors.UnsupportedGrantType);
        AssertDestructured(FindEventContaining(sink.Events, "Grant type is missing", LogEventLevel.Error));
    }

    [Fact]
    public async Task token_request_validation_details_should_be_destructured_by_serilog_when_authorization_code_invalid()
    {
        var (factory, sink) = CreatePipeline();
        using var _ = factory;
        var logger = factory.CreateLogger<TokenRequestValidator>();
        var validator = Factory.CreateTokenRequestValidator();
        SetLogger(validator, logger);
        var clients = Factory.CreateClientStore();
        var client = await clients.FindEnabledClientByIdAsync("codeclient", _ct);

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode },
            { OidcConstants.TokenRequest.Code, "not-a-real-code-handle" },
            { OidcConstants.TokenRequest.RedirectUri, "https://server/cb" },
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);

        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
        AssertDestructured(FindEventContaining(sink.Events, "Invalid authorization code", LogEventLevel.Error));
    }

    [Fact]
    public async Task token_request_validation_details_should_be_destructured_by_serilog_when_code_verifier_unexpected()
    {
        var (factory, sink) = CreatePipeline();
        using var _ = factory;
        var logger = factory.CreateLogger<TokenRequestValidator>();
        var (validator, parameters, clientResult) = await CreateAuthorizationCodeRequestAsync(logger, includeCodeVerifier: true, _ct);

        var result = await validator.ValidateRequestAsync(parameters, clientResult, _ct);

        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
        AssertDestructured(FindEventContaining(sink.Events, "Unexpected code_verifier", LogEventLevel.Error));
    }

    [Fact]
    public async Task token_request_validation_details_should_be_destructured_by_serilog_on_success()
    {
        var (factory, sink) = CreatePipeline();
        using var _ = factory;
        var logger = factory.CreateLogger<TokenRequestValidator>();
        var (validator, parameters, clientResult) = await CreateAuthorizationCodeRequestAsync(logger, includeCodeVerifier: false, _ct);

        var result = await validator.ValidateRequestAsync(parameters, clientResult, _ct);

        result.IsError.ShouldBeFalse();
        AssertDestructured(FindEventContaining(sink.Events, "Token request validation success", LogEventLevel.Information));
    }

    [Fact]
    public async Task authorize_request_validation_details_should_be_destructured_by_serilog()
    {
        var (factory, sink) = CreatePipeline();
        using var _ = factory;
        var logger = factory.CreateLogger<AuthorizeEndpointBaseTests.TestAuthorizeEndpoint>();
        var user = new IdentityServerUser("bob").CreatePrincipal();

        var validatedAuthorizeRequest = new ValidatedAuthorizeRequest
        {
            RedirectUri = "http://client/callback",
            State = "123",
            ResponseMode = "fragment",
            ClientId = "client",
            Client = new Client { ClientId = "client", ClientName = "Test Client" },
            Raw = [],
            Subject = user,
        };

        var stubValidator = new StubAuthorizeRequestValidator
        {
            Result = new AuthorizeRequestValidationResult(validatedAuthorizeRequest),
        };
        var stubResponseGenerator = new StubAuthorizeResponseGenerator
        {
            Response = new AuthorizeResponse { Request = validatedAuthorizeRequest },
        };

        var subject = new AuthorizeEndpointBaseTests.TestAuthorizeEndpoint(
            new TestEventService(),
            logger,
            new IdentityServerOptions(),
            stubValidator,
            new StubAuthorizeInteractionResponseGenerator(),
            stubResponseGenerator,
            new MockUserSession(),
            new MockConsentMessageStore());

        var result = await subject.ProcessAuthorizeRequestAsync(new NameValueCollection(), user, _ct);

        result.ShouldNotBeNull();
        AssertDestructured(FindEventContaining(sink.Events, "ValidatedAuthorizeRequest", LogEventLevel.Debug));
    }
}
