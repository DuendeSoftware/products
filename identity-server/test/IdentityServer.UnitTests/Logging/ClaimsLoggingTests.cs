// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace IdentityServer.UnitTests.Logging;

public sealed class ClaimsLoggingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void profile_request_looks_up_subject_regardless_of_log_level(bool enabled)
    {
        var identity = new CountingClaimsIdentity([new Claim(JwtClaimTypes.Subject, "123")]);
        var context = new ProfileDataRequestContext(
            new ClaimsPrincipal(identity),
            new Client { ClientId = "client", ClientName = "Client" },
            "caller",
            ["name"]);
        var logger = new ClaimsLogger(enabled);

        context.LogProfileRequest(logger);

        identity.SubjectLookups.ShouldBe(1);
        logger.Messages.Count.ShouldBe(enabled ? 1 : 0);
        if (enabled)
        {
            logger.Messages.Single().ShouldBe("Get profile called for subject 123 from application Client with claim types name via caller");
        }
    }

    [Fact]
    public void disabled_issued_claims_logging_does_not_allocate_or_write()
    {
        var context = new ProfileDataRequestContext
        {
            IssuedClaims = [new Claim("name", "Alice")]
        };
        var logger = new ClaimsLogger(false);
        context.LogIssuedClaims(logger);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            context.LogIssuedClaims(logger);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        allocated.ShouldBe(0);
        logger.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void enabled_issued_claims_logging_preserves_message()
    {
        var context = new ProfileDataRequestContext
        {
            IssuedClaims = [new Claim("name", "Alice"), new Claim("email", "alice@example.com")]
        };
        var logger = new ClaimsLogger(true);

        context.LogIssuedClaims(logger);

        logger.Messages.Single().ShouldBe("Issued claims: name, email");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task token_claims_only_perform_logging_subject_lookup_when_debug_enabled(bool enabled, bool identityToken)
    {
        var user = new IdentityServerUser("123")
        {
            IdentityProvider = "idp",
            AuthenticationTime = new DateTime(2000, 1, 1)
        }.CreatePrincipal();
        var identity = new CountingClaimsIdentity(user.Claims);
        var subject = new ClaimsPrincipal(identity);
        var logger = new ClaimsLogger(enabled);
        var service = new DefaultClaimsService(new MockProfileService(), logger);
        var request = new ValidatedRequest { Options = new IdentityServerOptions() };
        request.SetClient(new Client { ClientId = "client" });
        var resources = new ResourceValidationResult(new Resources());
        var ct = TestContext.Current.CancellationToken;

        var claims = identityToken
            ? await service.GetIdentityTokenClaimsAsync(subject, resources, false, request, ct)
            : await service.GetAccessTokenClaimsAsync(subject, resources, request, ct);

        identity.SubjectLookups.ShouldBe(enabled ? 2 : 1);
        claims.Single(x => x.Type == JwtClaimTypes.Subject).Value.ShouldBe("123");
        if (enabled)
        {
            logger.Messages.ShouldContain(identityToken
                ? "Getting claims for identity token for subject: 123 and client: client"
                : "Getting claims for access token for subject: 123");
        }
        else
        {
            logger.Messages.ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void protocol_claim_filter_only_enumerates_for_logging_when_debug_enabled(bool enabled)
    {
        var logger = new ClaimsLogger(enabled);
        var service = new FilteringClaimsService(logger);
        var enumerations = 0;
        var audienceClaim = new Claim(JwtClaimTypes.Audience, "api");
        var nameClaim = new Claim("name", "Alice");

        IEnumerable<Claim> GetClaims()
        {
            enumerations++;
            yield return audienceClaim;
            yield return nameClaim;
        }

        var claims = service.FilterClaims(GetClaims());

        if (enabled)
        {
            enumerations.ShouldBe(2);
            logger.Messages.Single().ShouldBe("Claim types from profile service that were filtered: aud");
        }
        else
        {
            enumerations.ShouldBe(0);
            logger.Messages.ShouldBeEmpty();
        }
        claims.Single().ShouldBeSameAs(nameClaim);
        enumerations.ShouldBe(enabled ? 4 : 2);
    }

    private sealed class CountingClaimsIdentity(IEnumerable<Claim> claims) : ClaimsIdentity(claims)
    {
        public int SubjectLookups { get; private set; }

        public override Claim? FindFirst(string type)
        {
            if (type == JwtClaimTypes.Subject)
            {
                SubjectLookups++;
            }
            return base.FindFirst(type);
        }
    }

    private sealed class FilteringClaimsService(ILogger<DefaultClaimsService> logger)
        : DefaultClaimsService(new MockProfileService(), logger)
    {
        public IEnumerable<Claim> FilterClaims(IEnumerable<Claim> claims) => FilterProtocolClaims(claims);
    }

    private sealed class ClaimsLogger(bool enabled) : ILogger<DefaultClaimsService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => enabled && logLevel == LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
