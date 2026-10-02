// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.Spaces;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Token;

public sealed class SpacesMtlsTokenEndpointTests(WebServerFixture webApp)
{
    private const string Category = "mTLS with Spaces";

    [Fact]
    [Trait("Category", Category)]
    public async Task path_discovery_isolates_issuer_and_mtls_aliases_per_space()
    {
        await using var fixture = new SpacesMtlsIdentityServerFixture(
            SpacesMtlsRoutingMode.Path, webApp, TestContext.Current.TestOutputHelper!);
        await fixture.InitializeAsync();

        await fixture.CreateSpaceAsync("Space A", new SpaceMatchPattern { Path = "/space-a" });
        await fixture.CreateSpaceAsync("Space B", new SpaceMatchPattern { Path = "/space-b" });

        using var client = fixture.CreateClient();

        var expectedA = CreatePathDiscoveryResult(fixture, "space-a");
        var expectedB = CreatePathDiscoveryResult(fixture, "space-b");

        var firstA = await FetchDiscoveryAsync(client, expectedA.DiscoveryEndpoint);
        firstA.ShouldBe(expectedA);

        var b = await FetchDiscoveryAsync(client, expectedB.DiscoveryEndpoint);
        b.ShouldBe(expectedB);

        var secondA = await FetchDiscoveryAsync(client, expectedA.DiscoveryEndpoint);
        secondA.ShouldBe(expectedA);

        secondA.ShouldBe(firstA);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task path_same_client_id_uses_space_specific_certificate()
    {
        await using var fixture = new SpacesMtlsIdentityServerFixture(
            SpacesMtlsRoutingMode.Path, webApp, TestContext.Current.TestOutputHelper!);
        await fixture.InitializeAsync();

        const string clientId = "mtls-client";
        const string scopeName = "space-scope";

        var spaceA = await fixture.CreateSpaceAsync("Space A", new SpaceMatchPattern { Path = "/space-a" });
        var spaceB = await fixture.CreateSpaceAsync("Space B", new SpaceMatchPattern { Path = "/space-b" });

        await fixture.AddApiScopeToSpaceAsync(spaceA, scopeName);
        await fixture.AddApiScopeToSpaceAsync(spaceB, scopeName);

        await fixture.AddMtlsClientToSpaceAsync(spaceA, clientId, fixture.CertA, scopeName);
        await fixture.AddMtlsClientToSpaceAsync(spaceB, clientId, fixture.CertB, scopeName);

        var discoClient = fixture.CreateClient();
        var expectedDiscoveryA = CreatePathDiscoveryResult(fixture, "space-a");
        var discoveryA = await FetchDiscoveryAsync(discoClient, expectedDiscoveryA.DiscoveryEndpoint);
        discoveryA.ShouldBe(expectedDiscoveryA);

        var expectedDiscoveryB = CreatePathDiscoveryResult(fixture, "space-b");
        var discoveryB = await FetchDiscoveryAsync(discoClient, expectedDiscoveryB.DiscoveryEndpoint);
        discoveryB.ShouldBe(expectedDiscoveryB);
        discoClient.Dispose();

        using var clientA = fixture.CreateMtlsClient(fixture.CertA);
        using var clientB = fixture.CreateMtlsClient(fixture.CertB);

        await AssertSuccessAsync(clientA, discoveryA.TokenEndpoint, clientId, scopeName, discoveryA.Issuer);
        await AssertRejectedAsync(clientA, discoveryB.TokenEndpoint, clientId, scopeName);
        await AssertSuccessAsync(clientB, discoveryB.TokenEndpoint, clientId, scopeName, discoveryB.Issuer);
        await AssertRejectedAsync(clientB, discoveryA.TokenEndpoint, clientId, scopeName);
        await AssertSuccessAsync(clientA, discoveryA.TokenEndpoint, clientId, scopeName, discoveryA.Issuer);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task path_client_ids_are_isolated_between_spaces_with_same_certificate()
    {
        await using var fixture = new SpacesMtlsIdentityServerFixture(
            SpacesMtlsRoutingMode.Path, webApp, TestContext.Current.TestOutputHelper!);
        await fixture.InitializeAsync();

        const string clientIdA = "partition-client-a";
        const string clientIdB = "partition-client-b";
        const string scopeName = "space-scope";

        var spaceA = await fixture.CreateSpaceAsync("Space A", new SpaceMatchPattern { Path = "/space-a" });
        var spaceB = await fixture.CreateSpaceAsync("Space B", new SpaceMatchPattern { Path = "/space-b" });

        await fixture.AddApiScopeToSpaceAsync(spaceA, scopeName);
        await fixture.AddApiScopeToSpaceAsync(spaceB, scopeName);

        await fixture.AddMtlsClientToSpaceAsync(spaceA, clientIdA, fixture.CertA, scopeName);
        await fixture.AddMtlsClientToSpaceAsync(spaceB, clientIdB, fixture.CertA, scopeName);

        using var discoveryClient = fixture.CreateClient();
        var expectedDiscoveryA = CreatePathDiscoveryResult(fixture, "space-a");
        var discoveryA = await FetchDiscoveryAsync(discoveryClient, expectedDiscoveryA.DiscoveryEndpoint);
        discoveryA.ShouldBe(expectedDiscoveryA);

        var expectedDiscoveryB = CreatePathDiscoveryResult(fixture, "space-b");
        var discoveryB = await FetchDiscoveryAsync(discoveryClient, expectedDiscoveryB.DiscoveryEndpoint);
        discoveryB.ShouldBe(expectedDiscoveryB);

        using var client = fixture.CreateMtlsClient(fixture.CertA);

        await AssertSuccessAsync(client, discoveryA.TokenEndpoint, clientIdA, scopeName, discoveryA.Issuer);
        await AssertRejectedAsync(client, discoveryB.TokenEndpoint, clientIdA, scopeName);
        await AssertSuccessAsync(client, discoveryB.TokenEndpoint, clientIdB, scopeName, discoveryB.Issuer);
        await AssertRejectedAsync(client, discoveryA.TokenEndpoint, clientIdB, scopeName);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task subdomain_discovery_isolates_issuer_and_mtls_aliases_per_space()
    {
        await using var fixture = new SpacesMtlsIdentityServerFixture(
            SpacesMtlsRoutingMode.Subdomain, webApp, TestContext.Current.TestOutputHelper!);
        await fixture.InitializeAsync();

        var canonicalA = fixture.GetSpaceSubdomainOrigin("space-a", useMtlsPrefix: false);
        var mtlsA = fixture.GetSpaceSubdomainOrigin("space-a", useMtlsPrefix: true);
        var canonicalB = fixture.GetSpaceSubdomainOrigin("space-b", useMtlsPrefix: false);
        var mtlsB = fixture.GetSpaceSubdomainOrigin("space-b", useMtlsPrefix: true);

        await fixture.CreateSpaceAsync("Space A",
            new SpaceMatchPattern { Origin = canonicalA.GetLeftPart(UriPartial.Authority) },
            new SpaceMatchPattern { Origin = mtlsA.GetLeftPart(UriPartial.Authority) });
        await fixture.CreateSpaceAsync("Space B",
            new SpaceMatchPattern { Origin = canonicalB.GetLeftPart(UriPartial.Authority) },
            new SpaceMatchPattern { Origin = mtlsB.GetLeftPart(UriPartial.Authority) });

        using var client = fixture.CreateClient();

        var expectedA = CreateSubdomainDiscoveryResult(canonicalA, mtlsA);
        var expectedB = CreateSubdomainDiscoveryResult(canonicalB, mtlsB);

        var firstA = await FetchDiscoveryAsync(client, expectedA.DiscoveryEndpoint);
        firstA.ShouldBe(expectedA);

        var b = await FetchDiscoveryAsync(client, expectedB.DiscoveryEndpoint);
        b.ShouldBe(expectedB);

        var secondA = await FetchDiscoveryAsync(client, expectedA.DiscoveryEndpoint);
        secondA.ShouldBe(expectedA);

        secondA.ShouldBe(firstA);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task subdomain_same_client_id_uses_space_specific_certificate()
    {
        await using var fixture = new SpacesMtlsIdentityServerFixture(
            SpacesMtlsRoutingMode.Subdomain, webApp, TestContext.Current.TestOutputHelper!);
        await fixture.InitializeAsync();

        const string clientId = "mtls-client";
        const string scopeName = "space-scope";

        var canonicalA = fixture.GetSpaceSubdomainOrigin("space-a", useMtlsPrefix: false);
        var mtlsA = fixture.GetSpaceSubdomainOrigin("space-a", useMtlsPrefix: true);
        var canonicalB = fixture.GetSpaceSubdomainOrigin("space-b", useMtlsPrefix: false);
        var mtlsB = fixture.GetSpaceSubdomainOrigin("space-b", useMtlsPrefix: true);

        var spaceA = await fixture.CreateSpaceAsync("Space A",
            new SpaceMatchPattern { Origin = canonicalA.GetLeftPart(UriPartial.Authority) },
            new SpaceMatchPattern { Origin = mtlsA.GetLeftPart(UriPartial.Authority) });
        var spaceB = await fixture.CreateSpaceAsync("Space B",
            new SpaceMatchPattern { Origin = canonicalB.GetLeftPart(UriPartial.Authority) },
            new SpaceMatchPattern { Origin = mtlsB.GetLeftPart(UriPartial.Authority) });

        await fixture.AddApiScopeToSpaceAsync(spaceA, scopeName);
        await fixture.AddApiScopeToSpaceAsync(spaceB, scopeName);

        await fixture.AddMtlsClientToSpaceAsync(spaceA, clientId, fixture.CertA, scopeName);
        await fixture.AddMtlsClientToSpaceAsync(spaceB, clientId, fixture.CertB, scopeName);

        var discoClient = fixture.CreateClient();
        var expectedDiscoveryA = CreateSubdomainDiscoveryResult(canonicalA, mtlsA);
        var discoveryA = await FetchDiscoveryAsync(discoClient, expectedDiscoveryA.DiscoveryEndpoint);
        discoveryA.ShouldBe(expectedDiscoveryA);

        var expectedDiscoveryB = CreateSubdomainDiscoveryResult(canonicalB, mtlsB);
        var discoveryB = await FetchDiscoveryAsync(discoClient, expectedDiscoveryB.DiscoveryEndpoint);
        discoveryB.ShouldBe(expectedDiscoveryB);
        discoClient.Dispose();

        using var clientA = fixture.CreateMtlsClient(fixture.CertA);
        using var clientB = fixture.CreateMtlsClient(fixture.CertB);

        await AssertSuccessAsync(clientA, discoveryA.TokenEndpoint, clientId, scopeName, discoveryA.Issuer);
        await AssertRejectedAsync(clientA, discoveryB.TokenEndpoint, clientId, scopeName);
        await AssertSuccessAsync(clientB, discoveryB.TokenEndpoint, clientId, scopeName, discoveryB.Issuer);
        await AssertRejectedAsync(clientB, discoveryA.TokenEndpoint, clientId, scopeName);
        await AssertSuccessAsync(clientA, discoveryA.TokenEndpoint, clientId, scopeName, discoveryA.Issuer);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task subdomain_client_ids_are_isolated_between_spaces_with_same_certificate()
    {
        await using var fixture = new SpacesMtlsIdentityServerFixture(
            SpacesMtlsRoutingMode.Subdomain, webApp, TestContext.Current.TestOutputHelper!);
        await fixture.InitializeAsync();

        const string clientIdA = "partition-client-a";
        const string clientIdB = "partition-client-b";
        const string scopeName = "space-scope";

        var canonicalA = fixture.GetSpaceSubdomainOrigin("space-a", useMtlsPrefix: false);
        var mtlsA = fixture.GetSpaceSubdomainOrigin("space-a", useMtlsPrefix: true);
        var canonicalB = fixture.GetSpaceSubdomainOrigin("space-b", useMtlsPrefix: false);
        var mtlsB = fixture.GetSpaceSubdomainOrigin("space-b", useMtlsPrefix: true);

        var spaceA = await fixture.CreateSpaceAsync("Space A",
            new SpaceMatchPattern { Origin = canonicalA.GetLeftPart(UriPartial.Authority) },
            new SpaceMatchPattern { Origin = mtlsA.GetLeftPart(UriPartial.Authority) });
        var spaceB = await fixture.CreateSpaceAsync("Space B",
            new SpaceMatchPattern { Origin = canonicalB.GetLeftPart(UriPartial.Authority) },
            new SpaceMatchPattern { Origin = mtlsB.GetLeftPart(UriPartial.Authority) });

        await fixture.AddApiScopeToSpaceAsync(spaceA, scopeName);
        await fixture.AddApiScopeToSpaceAsync(spaceB, scopeName);

        await fixture.AddMtlsClientToSpaceAsync(spaceA, clientIdA, fixture.CertA, scopeName);
        await fixture.AddMtlsClientToSpaceAsync(spaceB, clientIdB, fixture.CertA, scopeName);

        using var discoveryClient = fixture.CreateClient();
        var expectedDiscoveryA = CreateSubdomainDiscoveryResult(canonicalA, mtlsA);
        var discoveryA = await FetchDiscoveryAsync(discoveryClient, expectedDiscoveryA.DiscoveryEndpoint);
        discoveryA.ShouldBe(expectedDiscoveryA);

        var expectedDiscoveryB = CreateSubdomainDiscoveryResult(canonicalB, mtlsB);
        var discoveryB = await FetchDiscoveryAsync(discoveryClient, expectedDiscoveryB.DiscoveryEndpoint);
        discoveryB.ShouldBe(expectedDiscoveryB);

        using var client = fixture.CreateMtlsClient(fixture.CertA);

        await AssertSuccessAsync(client, discoveryA.TokenEndpoint, clientIdA, scopeName, discoveryA.Issuer);
        await AssertRejectedAsync(client, discoveryB.TokenEndpoint, clientIdA, scopeName);
        await AssertSuccessAsync(client, discoveryB.TokenEndpoint, clientIdB, scopeName, discoveryB.Issuer);
        await AssertRejectedAsync(client, discoveryA.TokenEndpoint, clientIdB, scopeName);
    }

    private static FocusedDiscoveryResult CreatePathDiscoveryResult(
        SpacesMtlsIdentityServerFixture fixture, string spaceSlug)
    {
        var origin = fixture.GetSpacePathOrigin(spaceSlug);
        return new FocusedDiscoveryResult(
            new Uri($"{origin}/.well-known/openid-configuration"),
            origin.AbsoluteUri,
            $"{origin}/connect/mtls/token",
            $"{origin}/connect/mtls/introspect",
            $"{origin}/connect/mtls/par");
    }

    private static FocusedDiscoveryResult CreateSubdomainDiscoveryResult(Uri canonicalOrigin, Uri mtlsOrigin) =>
        new(
            new Uri(canonicalOrigin, ".well-known/openid-configuration"),
            canonicalOrigin.GetLeftPart(UriPartial.Authority),
            new Uri(mtlsOrigin, "connect/token").AbsoluteUri,
            new Uri(mtlsOrigin, "connect/introspect").AbsoluteUri,
            new Uri(mtlsOrigin, "connect/par").AbsoluteUri);

    private static async Task<FocusedDiscoveryResult> FetchDiscoveryAsync(
        HttpClient client, Uri discoveryEndpoint)
    {
        var result = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = discoveryEndpoint.AbsoluteUri,
            Policy = { RequireKeySet = false }
        });
        result.IsError.ShouldBeFalse(result.Error);

        result.Issuer.ShouldNotBeNull();
        result.MtlsEndpointAliases.ShouldNotBeNull();
        result.MtlsEndpointAliases.TokenEndpoint.ShouldNotBeNull();
        result.MtlsEndpointAliases.IntrospectionEndpoint.ShouldNotBeNull();
        result.MtlsEndpointAliases.PushedAuthorizationRequestEndpoint.ShouldNotBeNull();

        return new FocusedDiscoveryResult(
            discoveryEndpoint,
            result.Issuer,
            result.MtlsEndpointAliases.TokenEndpoint,
            result.MtlsEndpointAliases.IntrospectionEndpoint,
            result.MtlsEndpointAliases.PushedAuthorizationRequestEndpoint);
    }

    private static async Task AssertSuccessAsync(
        HttpClient client, string tokenEndpoint, string clientId, string scopeName, string expectedIssuer)
    {
        var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = tokenEndpoint,
            ClientId = clientId,
            Scope = scopeName,
            ClientCredentialStyle = ClientCredentialStyle.PostBody
        });

        response.IsError.ShouldBeFalse(response.Error);
        response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        response.AccessToken.ShouldNotBeNullOrEmpty();
        response.TokenType.ShouldBe("Bearer");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        jwt.Issuer.ShouldBe(expectedIssuer);

        var scopeClaims = jwt.Claims.Where(c => c.Type == "scope").Select(c => c.Value).ToList();
        scopeClaims.ShouldContain(scopeName);
    }

    private static async Task AssertRejectedAsync(HttpClient client, string tokenEndpoint, string clientId, string scopeName)
    {
        var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = tokenEndpoint,
            ClientId = clientId,
            Scope = scopeName,
            ClientCredentialStyle = ClientCredentialStyle.PostBody
        });

        response.IsError.ShouldBeTrue();
        response.HttpStatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Error.ShouldBe("invalid_client");
        response.AccessToken.ShouldBeNullOrEmpty();
    }

    private sealed record FocusedDiscoveryResult(
        Uri DiscoveryEndpoint,
        string Issuer,
        string TokenEndpoint,
        string IntrospectionEndpoint,
        string ParEndpoint);
}
