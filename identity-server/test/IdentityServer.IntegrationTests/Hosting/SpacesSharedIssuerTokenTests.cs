// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.Spaces;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class SpacesSharedIssuerTokenTests(WebServerFixture webServerFixture)
{
    private const string ApiName = "api";
    private const string ApiSecret = "api-secret";
    private const string CallerId = "caller";
    private const string CallerSecret = "caller-secret";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Jwt_access_token_from_one_space_is_inactive_in_another_when_each_space_has_its_own_issuer()
    {
        await using var host = await CreateHostAsync(sharedIssuer: false);

        var token = await RequestAccessTokenAsync(host, host.SpaceA);

        (await IntrospectAsync(host, host.SpaceA, token)).ShouldBeTrue("the token is valid in the space that issued it");
        (await IntrospectAsync(host, host.SpaceB, token)).ShouldBeFalse();
    }

    [Fact]
    public async Task Jwt_access_token_from_one_space_is_active_in_another_when_spaces_share_an_issuer_and_signing_key()
    {
        await using var host = await CreateHostAsync(sharedIssuer: true);

        var token = await RequestAccessTokenAsync(host, host.SpaceA);

        (await IntrospectAsync(host, host.SpaceA, token)).ShouldBeTrue();
        (await IntrospectAsync(host, host.SpaceB, token)).ShouldBeTrue();
    }

    private Task<SpacesDataProtectionTestHost> CreateHostAsync(bool sharedIssuer) =>
        SpacesDataProtectionTestHost.CreateAsync(webServerFixture, nameof(SpacesSharedIssuerTokenTests),
            new SpacesDataProtectionTestHostOptions
            {
                ConfigureBuilder = builder => builder
                    .AddInMemoryApiScopes([new ApiScope(ApiName)])
                    .AddInMemoryApiResources([
                        new ApiResource(ApiName) { Scopes = { ApiName }, ApiSecrets = { new Secret(ApiSecret.Sha256()) } }
                    ])
                    .AddInMemoryClients([
                        new Client
                        {
                            ClientId = CallerId,
                            AllowedGrantTypes = GrantTypes.ClientCredentials,
                            ClientSecrets = { new Secret(CallerSecret.Sha256()) },
                            AllowedScopes = { ApiName },
                            AccessTokenType = AccessTokenType.Jwt
                        }
                    ]),
                ConfigureServices = services =>
                {
                    if (sharedIssuer)
                    {
                        _ = services.Configure<IdentityServerOptions>(o => o.IssuerUri = "https://shared-issuer.example");
                        _ = services.Configure<SpacesOptions>(o => o.AllowSharedIssuerUri = true);
                    }
                }
            }, _ct);

    private async Task<string> RequestAccessTokenAsync(SpacesDataProtectionTestHost host, SpacesTestSpace space)
    {
        var response = await host.CreateClient().RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = $"{space.Issuer}/connect/token",
            ClientId = CallerId,
            ClientSecret = CallerSecret,
            Scope = ApiName
        }, _ct);

        response.IsError.ShouldBeFalse(response.Error);
        return response.AccessToken!;
    }

    private async Task<bool> IntrospectAsync(SpacesDataProtectionTestHost host, SpacesTestSpace space, string token)
    {
        var response = await host.CreateClient().IntrospectTokenAsync(new TokenIntrospectionRequest
        {
            Address = $"{space.Issuer}/connect/introspect",
            ClientId = ApiName,
            ClientSecret = ApiSecret,
            Token = token
        }, _ct);

        response.IsError.ShouldBeFalse(response.Error);
        return response.IsActive;
    }
}
