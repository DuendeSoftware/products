// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
using Duende.AspNetCore.TestFramework;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit.Abstractions;

namespace Duende.AspNetCore.Authentication.JwtBearer;

public class DPoPIntegrationTests(ITestOutputHelper testOutputHelper)
{
    private Client DPoPOnlyClient = new()
    {
        ClientId = "client1",
        ClientSecrets = [new Secret("secret".ToSha256())],
        RequireDPoP = true,
        AllowedScopes = ["openid", "profile", "scope1"],
        AllowedGrantTypes = GrantTypes.Code,
        RedirectUris = ["https://app/signin-oidc"],
        PostLogoutRedirectUris = ["https://app/signout-callback-oidc"]
    };

    [Fact]
    [Trait("Category", "Integration")]
    public async Task missing_token_fails()
    {
        var api = await CreateDPoPApi();

        var result = await api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }


    [Fact]
    [Trait("Category", "Integration")]
    public async Task incorrect_token_type_fails()
    {
        var api = await CreateDPoPApi();
        var bearerToken = "unimportant opaque value";
        api.HttpClient.SetBearerToken(bearerToken);

        var result = await api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task valid_token_and_proof_succeeds()
    {
        var identityServer = await CreateIdentityServer();
        identityServer.Clients.Add(DPoPOnlyClient);
        var jwk = CreateJwk();
        var api = await CreateDPoPApi();

        var app = new AppHost(identityServer, api, "client1", testOutputHelper,
            configureUserTokenManagementOptions: opt => opt.DPoPJsonWebKey = jwk);
        await app.Initialize();

        // Login and get token for api call
        await app.LoginAsync("sub");
        var response = await app.BrowserClient.GetAsync(app.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserToken>();
        token.ShouldNotBeNull();
        token.AccessToken.ShouldNotBeNull();
        token.DPoPJsonWebKey.ShouldNotBeNull();
        api.HttpClient.SetToken(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP, token.AccessToken);

        // Create proof token for api call
        var dpopService =
            new DefaultDPoPProofService(new TestDPoPNonceStore(), new NullLogger<DefaultDPoPProofService>());
        var proof = await dpopService.CreateProofTokenAsync(new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            DPoPJsonWebKey = jwk,
            Method = "GET",
            Url = "http://localhost/"
        });
        proof.ShouldNotBeNull();
        api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof.ProofToken);

        var result = await api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task excessively_large_proof_fails()
    {
        var identityServer = await CreateIdentityServer(idsrv =>
        {
            idsrv.Clients.Add(DPoPOnlyClient);
        });

        var jwk = CreateJwk();
        var maxLength = 50;
        var api = await CreateDPoPApi(opt => opt.ProofTokenMaxLength = maxLength);

        var app = new AppHost(identityServer, api, "client1", testOutputHelper,
            configureUserTokenManagementOptions: opt => opt.DPoPJsonWebKey = jwk);
        await app.Initialize();

        // Login and get token for api call
        await app.LoginAsync("sub");
        var response = await app.BrowserClient.GetAsync(app.Url("/user_token"));
        var token = await response.Content.ReadFromJsonAsync<UserToken>();
        token.ShouldNotBeNull();
        token.AccessToken.ShouldNotBeNull();
        token.DPoPJsonWebKey.ShouldNotBeNull();
        api.HttpClient.SetToken(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP, token.AccessToken);

        // Create proof token for api call
        var dpopService =
            new DefaultDPoPProofService(new TestDPoPNonceStore(), new NullLogger<DefaultDPoPProofService>());
        var proof = await dpopService.CreateProofTokenAsync(new DPoPProofRequest
        {
            AccessToken = token.AccessToken,
            DPoPJsonWebKey = jwk,
            Method = "GET",
            Url = "http://localhost/",
            DPoPNonce = new string('x', maxLength + 1) // <--- Most important part of the test
        });
        proof.ShouldNotBeNull();
        api.HttpClient.DefaultRequestHeaders.Add(OidcConstants.HttpHeaders.DPoP, proof.ProofToken);

        var result = await api.HttpClient.GetAsync("/");

        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    public async Task<IdentityServerHost> CreateIdentityServer(Action<IdentityServerHost>? setup = null)
    {
        var host = new IdentityServerHost(testOutputHelper);
        setup?.Invoke(host);
        await host.Initialize();
        return host;
    }

    private async Task<ApiHost> CreateDPoPApi(Action<DPoPOptions>? configureDPoP = null)
    {
        var baseAddress = "https://api";
        var identityServer = await CreateIdentityServer();
        var api = new ApiHost(identityServer, testOutputHelper, baseAddress);
        api.OnConfigureServices += services =>
            services.ConfigureDPoPTokensForScheme(ApiHost.AuthenticationScheme,
                opt =>
                {
                    opt.TokenMode = DPoPMode.DPoPOnly;
                    configureDPoP?.Invoke(opt);
                });
        api.OnConfigure += app =>
            app.MapGet("/", () => "default route")
                .RequireAuthorization();
        await api.Initialize();
        return api;
    }

    private static string CreateJwk()
    {
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jwkKey = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
        jwkKey.Alg = "RS256";
        var jwk = JsonSerializer.Serialize(jwkKey);
        return jwk;
    }
}
