// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Web;
using Duende.IdentityModel;
using Duende.IdentityServer.Hosting.FederatedSignOut;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Test;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

/// <summary>
/// Isolated from <see cref="SamlFederatedSignoutTests"/> because it configures its own
/// downstream SAML service provider before the pipeline is initialized.
/// </summary>
public sealed class SamlFederatedSignoutDownstreamNotificationTests
{
    private const string Category = "SAML Federated Signout";
    private const string DownstreamSpEntityId = "https://downstream-sp.example.com";

    private readonly IdentityServerPipeline _pipeline = new();
    private readonly ClaimsPrincipal _user;

    public SamlFederatedSignoutDownstreamNotificationTests()
    {
        _user = new IdentityServerUser("bob")
        {
            AdditionalClaims = { new Claim(JwtClaimTypes.SessionId, "123") }
        }.CreatePrincipal();

        _pipeline.IdentityScopes.AddRange(new IdentityResource[]
        {
            new IdentityResources.OpenId()
        });

        _pipeline.Clients.Add(new Client
        {
            ClientId = "client1",
            AllowedGrantTypes = GrantTypes.Implicit,
            RequireConsent = false,
            AllowedScopes = new List<string> { "openid" },
            RedirectUris = new List<string> { "https://client1/callback" },
            FrontChannelLogoutUri = "https://client1/signout",
            PostLogoutRedirectUris = new List<string> { "https://client1/signout-callback" },
            AllowAccessTokensViaBrowser = true
        });

        _pipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Claims =
            [
                new Claim("name", "Bob Loblaw"),
                new Claim("email", "bob@loblaw.com")
            ]
        });

        _pipeline.SamlServiceProviders = new List<SamlServiceProvider>
        {
            new()
            {
                EntityId = DownstreamSpEntityId,
                AllowedScopes = new List<string> { "openid" },
                AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
                {
                    new()
                    {
                        Location = "https://downstream-sp.example.com/acs",
                        Binding = SamlBinding.HttpPost,
                        IsDefault = true
                    }
                },
                SingleLogoutServiceUrls = new HashSet<SamlEndpointType>
                {
                    new() { Location = "https://downstream-sp.example.com/slo", Binding = SamlBinding.HttpRedirect }
                }
            }
        };

        _pipeline.Initialize();
    }

    private static SamlSpLogoutContext CreateSamlContext() => new()
    {
        IdpEntityId = "https://upstream-idp.example.com",
        LogoutRequestId = "_req-abc-123",
        RelayState = "some-relay-state",
        ResponseBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Post",
        ResponseDestination = "https://upstream-idp.example.com/slo"
    };

    [Fact]
    [Trait("Category", Category)]
    public async Task saml_idp_initiated_logout_with_downstream_saml_sp_keys_session_by_bounded_correlation_id_while_handle_exceeds_200_chars()
    {
        await _pipeline.LoginAsync(_user);

        await _pipeline.RequestAuthorizationEndpointAsync(
            clientId: "client1",
            responseType: "id_token",
            scope: "openid",
            redirectUri: "https://client1/callback",
            state: "123_state",
            nonce: "123_nonce");

        _pipeline.OnFederatedSignout = async ctx =>
        {
            // Simulate a downstream SAML SP session recorded during SSO.
            var userSession = ctx.RequestServices.GetRequiredService<IUserSession>();
            await userSession.AddSamlSessionAsync(new SamlSpSessionData
            {
                EntityId = DownstreamSpEntityId,
                SessionIndex = "_sp-session-index",
                NameId = "bob"
            }, ctx.RequestAborted);

            await ctx.SignOutAsync();
            ctx.Items[SamlSpLogoutContext.HttpContextItemsKey] = CreateSamlContext();
            return true;
        };

        var response = await _pipeline.BrowserClient.GetAsync(
            IdentityServerPipeline.FederatedSignOutUrl + "?sid=123");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();

        // The completion-endpoint URL carries the protected message-store handle,
        // which is unbounded and routinely exceeds the SamlLogoutSession.LogoutId
        // column width (200 chars).
        var completionMatch = Regex.Match(html, "data-completion-url=\"([^\"]+)\"");
        completionMatch.Success.ShouldBeTrue();
        var completionUrl = HttpUtility.HtmlDecode(completionMatch.Groups[1].Value);
        var completionQuery = HttpUtility.ParseQueryString(new Uri(completionUrl).Query);
        var protectedHandle = completionQuery["logoutId"];
        protectedHandle.ShouldNotBeNullOrWhiteSpace();
        protectedHandle!.Length.ShouldBeGreaterThan(200);

        // Hitting the front-channel iframe (as the browser would) creates the
        // SamlLogoutSession, which must be keyed by the bounded correlation id,
        // not the protected handle.
        var iframeMatch = Regex.Match(html, "id=\"signout-frame\"[^>]*src=\"([^\"]+)\"");
        iframeMatch.Success.ShouldBeTrue();
        var iframeUrl = HttpUtility.HtmlDecode(iframeMatch.Groups[1].Value);
        await _pipeline.BrowserClient.GetAsync(iframeUrl);

        var samlLogoutSessionStore = _pipeline.Resolve<ISamlLogoutSessionStore>();

        // Never resolvable by the (long) protected handle.
        var byHandle = await samlLogoutSessionStore.GetByLogoutIdAsync(protectedHandle, TestContext.Current.CancellationToken);
        byHandle.ShouldBeNull();

        // Read back the correlation id embedded in the protected SamlSpLogoutMessage
        // to prove the session was actually stored under it.
        var samlSpLogoutMessageStore = _pipeline.Resolve<IMessageStore<SamlSpLogoutMessage>>();
        var storedMessage = await samlSpLogoutMessageStore.ReadAsync(protectedHandle, TestContext.Current.CancellationToken);
        storedMessage.ShouldNotBeNull();
        var correlationId = storedMessage.Data.SamlLogoutCorrelationId;
        correlationId.ShouldNotBeNullOrWhiteSpace();
        correlationId!.Length.ShouldBe(32);

        var byCorrelationId = await samlLogoutSessionStore.GetByLogoutIdAsync(correlationId, TestContext.Current.CancellationToken);
        byCorrelationId.ShouldNotBeNull();

        // Prove a real downstream notification was tracked (not an empty/skipped
        // session): the registered ISamlServiceProviderStore must have been
        // consulted and resolved the downstream SP, producing exactly one
        // expected response and zero skipped SPs.
        byCorrelationId!.SkippedSpCount.ShouldBe(0);
        byCorrelationId.ExpectedResponses.Count.ShouldBe(1);
        byCorrelationId.ExpectedResponses.Values.Single().SpEntityId.ShouldBe(DownstreamSpEntityId);
    }
}
