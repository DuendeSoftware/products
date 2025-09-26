// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EndToEndTests.TestInfra;
using ServiceDefaults;
using Xunit.Abstractions;
using WebClient = Duende.IdentityServer.EndToEndTests.TestInfra.WebClient;

namespace Duende.IdentityServer.EndToEndTests;

[Collection(IdentityServerAppHostCollection.CollectionName)]
public class IdentityServerTests : IdentityServerPlaywrightTestBase
{
    private readonly HttpClient _identityServerClient;
    private readonly WebClient _webClient;

    public IdentityServerTests(ITestOutputHelper output, IdentityServerHostTestFixture fixture) : base(output, fixture)
    {
        _identityServerClient = CreateHttpClient(AppHostServices.IdentityServer);
        _webClient = new WebClient(CreateHttpClient(AppHostServices.Web));
    }

    [Fact]
    public void Can_setup_fixture() => true.ShouldBeTrue();

    [Fact]
    public async Task Can_invoke_discovery()
    {
        var discoResponse = await _identityServerClient.GetAsync("/.well-known/openid-configuration");
        discoResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Can_login() => await _webClient.Login();
}
