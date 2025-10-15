// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EndToEndTests.TestInfra;
using Duende.Xunit.Playwright;
using Projects;
using ServiceDefaults;

namespace Duende.IdentityServer.EndToEndTests;

[Collection(IdentityServerAppHostCollection.CollectionName)]
public class IdentityServerTests(ITestOutputHelper output, IdentityServerHostTestFixture fixture)
    : PlaywrightTestBase<All>(output, fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(AppHostServices.MvcAutomaticTokenManagement)]
    [InlineData(AppHostServices.MvcCode)]
    [InlineData(AppHostServices.MvcDPoP)]
    [InlineData(AppHostServices.MvcHybridBackChannel)]
    [InlineData(AppHostServices.MvcJarJwt)]
    [InlineData(AppHostServices.MvcJarUriJwt)]
    [InlineData(AppHostServices.Web)]
    public async Task clients_can_login_use_tokens_and_logout(string clientName)
    {
        await Page.GotoAsync(Fixture.GetUrlTo(clientName).ToString());
        await Page.Login(ct: _ct);
        await Page.CallApi();
        await Page.RenewTokens();
        await Page.CallApi();
        await Page.Logout();
    }

    [Theory]
    [InlineData(AppHostServices.TemplateIs)]
    [InlineData(AppHostServices.TemplateIsEmpty)]
    [InlineData(AppHostServices.TemplateIsInMem)]
    [InlineData(AppHostServices.TemplateIsAspid)]
    // The EF template is disabled because we would need to run the migrations
    // [InlineData(AppHostServices.TemplateIsEF)]
    public async Task templates_can_serve_discovery(string templateName)
    {
        var client = CreateHttpClient(templateName);
        var response = await client.GetAsync(".well-known/openid-configuration", _ct);
        response.IsSuccessStatusCode.ShouldBeTrue();
    }
}
