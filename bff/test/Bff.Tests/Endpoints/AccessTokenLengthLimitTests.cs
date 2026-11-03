// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Duende.Bff.Tests.Endpoints;

/// <summary>
/// BFF no longer enforces any maximum length on access or refresh tokens. That's now the responsibility of
/// Duende.AccessTokenManagement (ATM), which is configurable via
/// <see cref="UserTokenManagementOptions.MaxAccessTokenLength"/> and
/// <see cref="ClientCredentialsTokenManagementOptions.MaxAccessTokenLength"/>. These tests exercise the real
/// token retrieval/forwarding pipeline (not a custom <c>IAccessTokenRetriever</c>), including the DPoP proof
/// generation path, which is where BFF's <c>AccessToken</c> -&gt; ATM <c>AccessToken</c> conversion happens
/// (see <c>Duende.Bff.AccessTokenManagement.AccessToken</c>'s implicit operator).
/// </summary>
public class AccessTokenLengthLimitTests : BffTestBase
{
    private const string BigClaimType = "big_claim";

    // Set by a test before calling InitializeAsync(), since the client must be marked as requiring DPoP
    // before it's added to the IdentityServer client collection (which happens in our InitializeAsync override).
    private bool _requireDPoP;

    // One claim this size pushes the resulting JWT comfortably over the former 32kb limit,
    // once JSON-encoded and base64url-encoded into the token.
    private static string BigClaimValue(int size = 34 * 1024) => new('a', size);

    public override async ValueTask InitializeAsync()
    {
        var idSrvClient = IdentityServer.AddClient(The.ClientId, Bff.Url());
        idSrvClient.RequireDPoP = _requireDPoP;

        // Make the user access token big: the default profile service forwards any claims on the
        // signed-in principal that are listed in the requested scope's UserClaims.
        IdentityServer.ApiScopes.Add(new ApiScope(The.Scope, [BigClaimType]));
        IdentityServer.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypes.Name, The.UserName),
            new Claim(JwtClaimTypes.Subject, The.Sub),
            new Claim(BigClaimType, BigClaimValue())
        ], "test", "name", "role"));

        // Make the client-credentials access token big via a client claim.
        idSrvClient.Claims.Add(new ClientClaim(BigClaimType, BigClaimValue()));
        idSrvClient.AlwaysSendClientClaims = true;
        idSrvClient.ClientClaimsPrefix = "";

        Bff.OnConfigureBff += bff => bff.AddRemoteApis();

        await base.InitializeAsync();

        if (_requireDPoP)
        {
            Bff.BffOptions.DPoPJsonWebKey = The.DPoPJsonWebKey;
        }

        Bff.BffOptions.ConfigureOpenIdConnectDefaults = opt =>
        {
            opt.BackchannelHttpHandler = Internet;
            The.DefaultOpenIdConnectConfiguration(opt);
        };
    }

    [Fact]
    public async Task with_raised_limit_big_user_access_token_reaches_api_as_bearer_token()
    {
        Bff.OnConfigureServices += services =>
            services.Configure<UserTokenManagementOptions>(o => o.MaxAccessTokenLength = 64 * 1024);

        Bff.OnConfigureApp += app =>
            _ = app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.User);

        await InitializeAsync();

        _ = await Bff.BrowserClient.Login();

        ApiCallDetails callToApi = await Bff.BrowserClient.CallBffHostApi(url: Bff.Url(The.PathAndSubPath));

        callToApi.Sub.ShouldBe(The.Sub);
        var authorizationHeader = callToApi.RequestHeaders["Authorization"].Single();
        authorizationHeader.ShouldStartWith("Bearer ");
        authorizationHeader.Length.ShouldBeGreaterThan(32 * 1024);
    }

    [Fact]
    public async Task with_raised_limit_big_user_access_token_reaches_api_via_dpop()
    {
        _requireDPoP = true;

        Bff.OnConfigureServices += services =>
            services.Configure<UserTokenManagementOptions>(o => o.MaxAccessTokenLength = 64 * 1024);

        Api.OnConfigureServices += services =>
            services.ConfigureDPoPTokensForScheme("token", o =>
            {
                o.EnableReplayDetection = false;
                // The API's own DPoP validation has its own, independent length limit on the access token it accepts.
                o.ProofTokenMaxLength = 64 * 1024;
            });

        Bff.OnConfigureApp += app =>
            _ = app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.User);

        await InitializeAsync();

        _ = await Bff.BrowserClient.Login();

        ApiCallDetails callToApi = await Bff.BrowserClient.CallBffHostApi(url: Bff.Url(The.PathAndSubPath));

        callToApi.Sub.ShouldBe(The.Sub);
        callToApi.RequestHeaders["DPoP"].First().ShouldNotBeNullOrEmpty();
        var authorizationHeader = callToApi.RequestHeaders["Authorization"].Single();
        authorizationHeader.ShouldStartWith("DPoP ");
        authorizationHeader.Length.ShouldBeGreaterThan(32 * 1024);
    }

    [Fact]
    public async Task with_raised_limit_big_client_credentials_token_reaches_api_as_bearer_token()
    {
        Bff.OnConfigureServices += services =>
            services.Configure<ClientCredentialsTokenManagementOptions>(o => o.MaxAccessTokenLength = 64 * 1024);

        Bff.OnConfigureApp += app =>
            _ = app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.Client);

        await InitializeAsync();

        ApiCallDetails callToApi = await Bff.BrowserClient.CallBffHostApi(url: Bff.Url(The.PathAndSubPath));

        callToApi.ClientId.ShouldBe(The.ClientId);
        var authorizationHeader = callToApi.RequestHeaders["Authorization"].Single();
        authorizationHeader.ShouldStartWith("Bearer ");
        authorizationHeader.Length.ShouldBeGreaterThan(32 * 1024);
    }

    [Fact]
    public async Task with_raised_limit_big_client_credentials_token_reaches_api_via_dpop()
    {
        _requireDPoP = true;

        Bff.OnConfigureServices += services =>
            services.Configure<ClientCredentialsTokenManagementOptions>(o => o.MaxAccessTokenLength = 64 * 1024);

        Api.OnConfigureServices += services =>
            services.ConfigureDPoPTokensForScheme("token", o =>
            {
                o.EnableReplayDetection = false;
                // The API's own DPoP validation has its own, independent length limit on the access token it accepts.
                o.ProofTokenMaxLength = 64 * 1024;
            });

        Bff.OnConfigureApp += app =>
            _ = app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.Client);

        await InitializeAsync();

        ApiCallDetails callToApi = await Bff.BrowserClient.CallBffHostApi(url: Bff.Url(The.PathAndSubPath));

        callToApi.ClientId.ShouldBe(The.ClientId);
        callToApi.RequestHeaders["DPoP"].First().ShouldNotBeNullOrEmpty();
        var authorizationHeader = callToApi.RequestHeaders["Authorization"].Single();
        authorizationHeader.ShouldStartWith("DPoP ");
        authorizationHeader.Length.ShouldBeGreaterThan(32 * 1024);
    }

    [Fact]
    public async Task with_default_limit_big_user_access_token_is_rejected_by_atm()
    {
        // Guards existing ATM behaviour (not BFF's): with the default
        // UserTokenManagementOptions.MaxAccessTokenLength (32kb), ATM refuses to hand back an oversized
        // access token. The exception propagates out of the YARP transform, so the call to the remote API
        // never happens and the BFF returns a 502 (YARP's RequestCreation failure handling).
        Bff.OnConfigureApp += app =>
            _ = app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.User);

        await InitializeAsync();

        _ = await Bff.BrowserClient.Login();

        _ = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.PathAndSubPath),
            expectedStatusCode: HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task with_default_limit_big_client_credentials_token_is_rejected_by_atm()
    {
        // Guards existing ATM behaviour (not BFF's): with the default
        // ClientCredentialsTokenManagementOptions.MaxAccessTokenLength (32kb), ATM refuses to hand back an
        // oversized access token. The exception propagates out of the YARP transform, so the call to the
        // remote API never happens and the BFF returns a 502 (YARP's RequestCreation failure handling).
        Bff.OnConfigureApp += app =>
            _ = app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.Client);

        await InitializeAsync();

        _ = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.PathAndSubPath),
            expectedStatusCode: HttpStatusCode.BadGateway);
    }
}



