// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
namespace Duende.Bff.Tests.Endpoints;

public class DPoPRemoteEndpointTests : BffTestBase
{
    public override async ValueTask InitializeAsync()
    {
        var idSrvClient = IdentityServer.AddClient(The.ClientId, Bff.Url());

        idSrvClient.RequireDPoP = true;

        Bff.OnConfigureBff += bff => bff.AddRemoteApis();


        await base.InitializeAsync();
        Bff.BffOptions.DPoPJsonWebKey = The.DPoPJsonWebKey;
        Bff.BffOptions.ConfigureOpenIdConnectDefaults = opt =>
        {
            opt.BackchannelHttpHandler = Internet;
            The.DefaultOpenIdConnectConfiguration(opt);
        };
    }

    [Fact]
    public async Task Can_call_dpop_protected_api_with_user_token()
    {
        Api.OnConfigureServices += services =>
        {
            services.ConfigureDPoPTokensForScheme("token");
        };

        Bff.OnConfigureApp += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.User);
        };

        await InitializeAsync();

        await Bff.BrowserClient.Login()
            .CheckHttpStatusCode();

        ApiCallDetails callToApi = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.PathAndSubPath)
        );

        callToApi.RequestHeaders["DPoP"].First().ShouldNotBeNullOrEmpty();
        callToApi.RequestHeaders["Authorization"].First().StartsWith("DPoP ").ShouldBeTrue();
        callToApi.Sub.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Can_call_dpop_protected_api_with_client_token()
    {
        Api.OnConfigureServices += services =>
        {
            services.ConfigureDPoPTokensForScheme("token");
        };

        Bff.OnConfigureApp += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url())
                .WithAccessToken(RequiredTokenType.Client);
        };

        await InitializeAsync();

        ApiCallDetails callToApi = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.PathAndSubPath)
        );

        callToApi.RequestHeaders["DPoP"].First().ShouldNotBeNullOrEmpty();
        callToApi.RequestHeaders["Authorization"].First().StartsWith("DPoP ").ShouldBeTrue();
        callToApi.ClientId.ShouldNotBeNullOrEmpty("this clientid would be empty if dpop validation failes");
    }

    [Theory]
    [InlineData("/api/foo", "api/foo", "api/foo/bar")]
    [InlineData("/api/foo", "api/foo", "api/foo")]
    [InlineData("/api/foo", "foo", "api/foo")]
    public async Task HTU_values_are_correctly_verified_for_paths_and_subpaths(string mappedPath, string targetPath, string calledPath)
    {
        Api.OnConfigureServices += services =>
        {
            services.ConfigureDPoPTokensForScheme("token");
        };

        Bff.OnConfigureApp += app =>
        {
            app.MapRemoteBffApiEndpoint(mappedPath, Api.Url(targetPath))
                .WithAccessToken(RequiredTokenType.Client);
        };

        await InitializeAsync();

        ApiCallDetails callToApi = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(calledPath)
        );

        callToApi.RequestHeaders["DPoP"].First().ShouldNotBeNullOrEmpty();
        callToApi.RequestHeaders["Authorization"].First().StartsWith("DPoP ").ShouldBeTrue();
        callToApi.ClientId.ShouldNotBeNullOrEmpty("this clientid would be empty if dpop validation failes");
    }

    [Fact]
    public async Task DPoP_htu_matches_yarp_destination_when_api_has_path_prefix()
    {
        // This test reproduces the issue described in https://github.com/orgs/DuendeSoftware/discussions/461
        // When mapping /api/foo to https://example.com/api/foo, a request to /api/foo/bar should
        // result in DPoP HTU of https://example.com/api/foo/bar (not https://example.com/bar)

        string? capturedDPoPHeader = null;
        string? capturedRequestPath = null;

        Api.OnConfigureServices += services =>
        {

            services.ConfigureDPoPTokensForScheme("token");
        };

        Api.OnConfigureApp += app =>
        {
            app.Use(async (context, next) =>
            {
                capturedDPoPHeader = context.Request.Headers["DPoP"].FirstOrDefault();
                capturedRequestPath = context.Request.Path.Value;
                await next();
            });
        };

        Bff.OnConfigureApp += app =>
        {
            // Map BFF /api/foo to API https://localhost:port/api/foo
            app.MapRemoteBffApiEndpoint("/api/foo", Api.Url("api/foo"))
                .WithAccessToken(RequiredTokenType.Client);
        };

        await InitializeAsync();

        // Make a request to /api/foo/bar
        ApiCallDetails callToApi = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api/foo/bar")
        );

        // Verify the API received the request at the correct path
        capturedRequestPath.ShouldBe("/api/foo/bar");

        // Parse the DPoP proof token to extract the HTU claim
        capturedDPoPHeader.ShouldNotBeNullOrEmpty();
        var dpopToken = capturedDPoPHeader!;

        // The DPoP token is a JWT with format: header.payload.signature
        var parts = dpopToken.Split('.');
        parts.Length.ShouldBe(3);

        // Decode the payload (base64url)
        var payload = System.Text.Json.JsonDocument.Parse(
            Convert.FromBase64String(Base64UrlDecode(parts[1]))
        );

        var htu = payload.RootElement.GetProperty("htu").GetString();

        // The HTU should match the actual destination URL that YARP sent the request to
        // Expected: https://localhost:port/api/foo/bar
        // Bug would produce: https://localhost:port/bar
        htu.ShouldBe(Api.Url("api/foo/bar").ToString());
    }

    private static string Base64UrlDecode(string input)
    {
        var output = input;
        output = output.Replace('-', '+').Replace('_', '/');

        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }

        return output;
    }
}
