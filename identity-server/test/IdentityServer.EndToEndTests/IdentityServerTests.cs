// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EndToEndTests.TestInfra;
using Duende.Xunit.Playwright;
using Projects;
using ServiceDefaults;
using Xunit.Abstractions;

namespace Duende.IdentityServer.EndToEndTests;

[Collection(IdentityServerAppHostCollection.CollectionName)]
public class IdentityServerTests : PlaywrightTestBase<All>
{
    private readonly HttpClient _identityServerClient;
    private readonly HttpClient _webClient;

    public IdentityServerTests(ITestOutputHelper output, IdentityServerHostTestFixture fixture) : base(output, fixture)
    {
        _identityServerClient = CreateHttpClient(AppHostServices.IdentityServer);
        _webClient = CreateHttpClient(AppHostServices.Web);
    }

    [Theory]
    [InlineData(AppHostServices.MvcAutomaticTokenManagement)]
    [InlineData(AppHostServices.MvcCode)]
    [InlineData(AppHostServices.MvcDPoP)]
    [InlineData(AppHostServices.MvcHybridBackChannel)]
    [InlineData(AppHostServices.MvcJarJwt)]
    [InlineData(AppHostServices.MvcJarUriJwt)]
    [InlineData(AppHostServices.Web)]
    public async Task can_login_use_tokens_and_logout(string clientName)
    {
        await Page.GotoAsync(Fixture.GetUrlTo(clientName).ToString());
        await Page.Login();
        await Page.CallApi();
        await Page.RenewTokens();
        await Page.CallApi();
        await Page.Logout();
    }
}
