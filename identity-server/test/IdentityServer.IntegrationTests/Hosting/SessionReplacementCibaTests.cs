// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Test;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

/// <summary>
/// Regression tests for session replacement correctly coordinating CIBA-only clients, i.e.
/// clients that are never added to the session's client list (because CIBA completion does
/// not call IUserSession.AddClientIdAsync) but that do have persisted grants tied to the
/// session's subject and session id.
/// </summary>
public sealed class SessionReplacementCibaTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly IdentityServerPipeline _pipeline = new();
    private readonly MockCibaUserValidator _mockCibaUserValidator = new();
    private readonly MockCibaUserNotificationService _mockCibaUserNotificationService = new();

    private const string CoordinatedCibaClientId = "ciba_coordinated";
    private const string OptedOutCibaClientId = "ciba_opted_out";

    public SessionReplacementCibaTests()
    {
        _pipeline.OnPostConfigureServices += s =>
        {
            s.AddIdentityServerBuilder().AddServerSideSessions();
            s.AddSingleton<IBackchannelAuthenticationUserValidator>(_mockCibaUserValidator);
            s.AddSingleton<IBackchannelAuthenticationUserNotificationService>(_mockCibaUserNotificationService);
        };

        _pipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
        });
        _pipeline.Users.Add(new TestUser
        {
            SubjectId = "alice",
            Username = "alice",
        });

        _pipeline.Clients.Add(new Client
        {
            ClientId = CoordinatedCibaClientId,
            AllowedGrantTypes = GrantTypes.Ciba,
            ClientSecrets = { new Secret("secret".Sha256()) },
            CoordinateLifetimeWithUserSession = true,
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "api" },
        });
        _pipeline.Clients.Add(new Client
        {
            ClientId = OptedOutCibaClientId,
            AllowedGrantTypes = GrantTypes.Ciba,
            ClientSecrets = { new Secret("secret".Sha256()) },
            CoordinateLifetimeWithUserSession = false,
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "api" },
        });

        _pipeline.IdentityScopes.Add(new IdentityResources.OpenId());
        _pipeline.ApiScopes.Add(new ApiScope("api"));
    }

    private void SetValidatedUser(string sub) =>
        _mockCibaUserValidator.Result.Subject = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", sub)], "ciba"));

    private async Task<string> CreateCibaRequestAsync(string clientId, string subjectId)
    {
        SetValidatedUser(subjectId);

        var cibaBody = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", "secret" },
            { "scope", "openid api" },
            { "login_hint", "this means " + subjectId },
            { "binding_message", Guid.NewGuid().ToString("n") },
        };

        var cibaResponse = await _pipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.BackchannelAuthenticationEndpoint,
            new FormUrlEncodedContent(cibaBody));
        cibaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var values = JsonSerializer.Deserialize<Dictionary<string, object>>(await cibaResponse.Content.ReadAsStringAsync());
        return values["auth_req_id"].ToString();
    }

    /// <summary>
    /// Completes the most recently created backchannel login request, tying it to the
    /// previously signed-in browser session's subject and session id, the same way a real
    /// CIBA completion would (without ever calling IUserSession.AddClientIdAsync).
    /// </summary>
    private async Task CompletePendingLoginRequestAsync(string subjectId, string sessionId)
    {
        var cibaService = _pipeline.Resolve<IBackchannelAuthenticationInteractionService>();
        var request = await cibaService.GetLoginRequestByInternalIdAsync(_mockCibaUserNotificationService.LoginRequest.InternalId, _ct);
        await cibaService.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(_mockCibaUserNotificationService.LoginRequest.InternalId)
        {
            ScopesValuesConsented = request.ValidatedResources.RawScopeValues,
            Subject = new IdentityServerUser(subjectId)
            {
                AuthenticationTime = DateTime.UtcNow,
                IdentityProvider = IdentityServerConstants.LocalIdentityProvider,
            }.CreatePrincipal(),
            SessionId = sessionId,
        }, _ct);
    }

    private async Task<HttpResponseMessage> RedeemAsync(string clientId, string requestId) =>
        await _pipeline.BackChannelClient.PostAsync(
            IdentityServerPipeline.TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", "secret" },
                { "grant_type", "urn:openid:params:grant-type:ciba" },
                { "auth_req_id", requestId },
            }));

    [Fact]
    public async Task session_replacement_revokes_completed_ciba_request_for_coordinated_client_not_in_session_client_list()
    {
        _pipeline.Initialize();
        var sessionStore = _pipeline.Resolve<IServerSideSessionStore>();

        await _pipeline.LoginAsync("bob");
        var bobSessionId = (await sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).Single().SessionId;

        var coordinatedRequestId = await CreateCibaRequestAsync(CoordinatedCibaClientId, "bob");
        await CompletePendingLoginRequestAsync("bob", bobSessionId);

        var optedOutRequestId = await CreateCibaRequestAsync(OptedOutCibaClientId, "bob");
        await CompletePendingLoginRequestAsync("bob", bobSessionId);

        // Neither CIBA client was ever added to the session's client list: CompleteLoginRequestAsync
        // only sets request.SessionId, it never calls IUserSession.AddClientIdAsync.

        // session replacement: alice signs in on the same browser, replacing bob's session
        await _pipeline.LoginAsync("alice");

        var coordinatedTokenResponse = await RedeemAsync(CoordinatedCibaClientId, coordinatedRequestId);
        coordinatedTokenResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var coordinatedJson = JsonSerializer.Deserialize<Dictionary<string, object>>(await coordinatedTokenResponse.Content.ReadAsStringAsync());
        coordinatedJson["error"].ToString().ShouldBe("invalid_grant");

        var optedOutTokenResponse = await RedeemAsync(OptedOutCibaClientId, optedOutRequestId);
        optedOutTokenResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
