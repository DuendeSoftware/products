// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EndToEndTests.TestInfra;
using Duende.Xunit.Playwright;
using Projects;
using ServiceDefaults;
using Xunit.Abstractions;

namespace Duende.IdentityServer.EndToEndTests;

[Collection(IdentityServerAppHostCollection.CollectionName)]
public class IdentityServerTests : IntegrationTestBase<Dev>
{
    private readonly HttpClient _identityServerClient;
    private readonly HttpClient _webClient;

    public IdentityServerTests(ITestOutputHelper output, IdentityServerHostTestFixture fixture) : base(output, fixture)
    {
        _identityServerClient = CreateHttpClient(AppHostServices.IdentityServer);
        _webClient = CreateHttpClient(AppHostServices.Web);
    }

    [Fact]
    public void Can_setup_fixture() => true.ShouldBeTrue();

    [Fact]
    public async Task Can_invoke_discovery()
    {
        var discoResponse = await _identityServerClient.GetAsync("/.well-known/openid-configuration");
        discoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var webResponse = await _webClient.GetAsync("/");
        webResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
