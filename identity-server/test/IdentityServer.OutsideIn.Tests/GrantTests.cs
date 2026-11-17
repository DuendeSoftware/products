// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.OutsideIn.Tests;

/// <summary>
/// Grants issued on one node can be used on the other, because operational data (codes, refresh tokens, reference
/// tokens) is shared by both nodes of the system under test, whatever the store type (design doc D4, D6, D36): every
/// request in these scenarios goes through the load balancer, which round-robins between the two nodes.
/// </summary>
public sealed class GrantTests
{
    /// <summary>Catalog: RT-01.</summary>
    [Theory, Matrix(Dimensions.Stores | Dimensions.Caching | Dimensions.Users | Dimensions.Spaces)]
    public async Task Offline_access_returns_refresh_token_that_can_be_redeemed(Permutation p)
    {
        await using var env = await TestEnvironment.StartAsync(p, env =>
        {
            env.SetupIdentityServer(ids => ids.WithPasswordLogin());
            return env.SetupPerIssuer(issuer =>
            {
                var alice = issuer.TestUsers.Create("alice");
                var clientApp = env.AddClientApp("web", app => app.UseAuthority(issuer));
                var client = issuer.TestClients.CodeFlow(clientApp).WithRefreshTokens().WithManualCallback(clientApp);
                return (alice, clientApp, client);
            });
        });

        await env.SetupData(async (issuer, r) =>
        {
            await issuer.Storage.AddDefaultsAsync();
            await issuer.Storage.AddUserAsync(r.alice);
            await issuer.Storage.AddClientAsync(r.client);
        });

        var browser = env.CreateBrowser();
        var backChannel = env.CreateBackChannelClient();

        await env.VerifyScenario(async (issuer, r) =>
        {
            var tokenRequest = await AuthorizeAsync(issuer, browser, r.alice, r.client, r.clientApp, "openid offline_access");
            var tokenResponse = await backChannel.RequestAuthorizationCodeTokenAsync(tokenRequest);

            var refreshResponse = await backChannel.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = issuer.TokenEndpoint.ToString(),
                ClientId = r.client.ClientId,
                ClientSecret = r.clientApp.ClientSecret,
                RefreshToken = tokenResponse.RefreshToken,
            });

            return new Dictionary<string, object>
            {
                ["AuthorizationCodeRedemption"] = tokenResponse.HttpResponse,
                ["RefreshTokenRedemption"] = refreshResponse.HttpResponse,
            };
        });
    }

    /// <summary>Catalog: TOK-02.</summary>
    [Theory, Matrix(Dimensions.Stores | Dimensions.Caching | Dimensions.Users | Dimensions.Spaces)]
    public async Task Api_introspects_reference_token_and_gets_active_with_claims(Permutation p)
    {
        await using var env = await TestEnvironment.StartAsync(p, env =>
        {
            env.SetupIdentityServer(ids => ids.WithPasswordLogin());
            return env.SetupPerIssuer(issuer =>
            {
                var alice = issuer.TestUsers.Create("alice");
                var clientApp = env.AddClientApp("web", app => app.UseAuthority(issuer));
                var client = issuer.TestClients.CodeFlow(clientApp).WithReferenceTokens().WithManualCallback(clientApp);
                var (resource, secret) = issuer.TestResources.ApiResourceWithSecret("api", "api1");
                return (alice, clientApp, client, resource, secret);
            });
        });

        await env.SetupData(async (issuer, r) =>
        {
            await issuer.Storage.AddDefaultsAsync();
            await issuer.Storage.AddUserAsync(r.alice);
            await issuer.Storage.AddClientAsync(r.client);
            await issuer.Storage.AddApiResourceAsync(r.resource);
        });

        var browser = env.CreateBrowser();
        var backChannel = env.CreateBackChannelClient();

        await env.VerifyScenario(async (issuer, r) =>
        {
            var tokenRequest = await AuthorizeAsync(issuer, browser, r.alice, r.client, r.clientApp, "openid api1");
            var tokenResponse = await backChannel.RequestAuthorizationCodeTokenAsync(tokenRequest);

            var introspectionResponse = await backChannel.IntrospectTokenAsync(new TokenIntrospectionRequest
            {
                Address = issuer.IntrospectionEndpoint.ToString(),
                ClientId = r.resource.Name,
                ClientSecret = r.secret,
                Token = tokenResponse.AccessToken,
            });

            return introspectionResponse.HttpResponse;
        });
    }

    /// <summary>Catalog: OIDC-06.</summary>
    [Theory, Matrix(Dimensions.Stores | Dimensions.Caching | Dimensions.Users | Dimensions.Spaces)]
    public async Task Authorization_code_is_single_use(Permutation p)
    {
        await using var env = await TestEnvironment.StartAsync(p, env =>
        {
            env.SetupIdentityServer(ids => ids.WithPasswordLogin());
            return env.SetupPerIssuer(issuer =>
            {
                var alice = issuer.TestUsers.Create("alice");
                var clientApp = env.AddClientApp("web", app => app.UseAuthority(issuer));
                var client = issuer.TestClients.CodeFlow(clientApp).WithManualCallback(clientApp);
                return (alice, clientApp, client);
            });
        });

        await env.SetupData(async (issuer, r) =>
        {
            await issuer.Storage.AddDefaultsAsync();
            await issuer.Storage.AddUserAsync(r.alice);
            await issuer.Storage.AddClientAsync(r.client);
        });

        var browser = env.CreateBrowser();
        var backChannel = env.CreateBackChannelClient();

        await env.VerifyScenario(async (issuer, r) =>
        {
            var tokenRequest = await AuthorizeAsync(issuer, browser, r.alice, r.client, r.clientApp, "openid");

            var firstRedemption = await backChannel.RequestAuthorizationCodeTokenAsync(tokenRequest);
            var secondRedemption = await backChannel.RequestAuthorizationCodeTokenAsync(tokenRequest);

            return new Dictionary<string, object>
            {
                ["FirstRedemption"] = firstRedemption.HttpResponse,
                ["SecondRedemption"] = secondRedemption.HttpResponse,
            };
        });
    }

    // Shared by every scenario here: drives the authorize request by hand and captures the code (see
    // TestBrowser.AuthorizeAsync), then prepares a token request ready to redeem it. Each scenario still redeems it
    // (once, or twice for OIDC-06) itself, since that's the behavior under test.
    private static async Task<AuthorizationCodeTokenRequest> AuthorizeAsync(
        IIssuer issuer, TestBrowser browser, TestUser user, Client client, IWebApp clientApp, string scope)
    {
        var codeVerifier = Pkce.NewCodeVerifier();
        var authorizeUrl = issuer.CodeFlowUrl(client, scope, clientApp.ManualCallbackUrl, state: "s1", Pkce.Challenge(codeVerifier));
        var authorized = await browser.AuthorizeAsync(authorizeUrl, user);

        return new AuthorizationCodeTokenRequest
        {
            Address = issuer.TokenEndpoint.ToString(),
            ClientId = client.ClientId,
            ClientSecret = clientApp.ClientSecret,
            Code = authorized.Code,
            RedirectUri = clientApp.ManualCallbackUrl.ToString(),
            CodeVerifier = codeVerifier,
        };
    }
}
