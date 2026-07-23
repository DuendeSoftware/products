// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EndToEndTests.TestInfra;
using Duende.Xunit.Playwright;
using Projects;
using ServiceDefaults;

namespace Duende.IdentityServer.EndToEndTests;

[Collection(IdentityServerAppHostCollection.CollectionName)]
public class IdentityServerTests(IdentityServerHostTestFixture fixture)
    : PlaywrightTestBase<All>(fixture)
{
    // Client login E2E tests have been moved to the interactive scenario test harness
    // (IdentityServer.Interaction.Scenarios / IdentityServer.Interaction.Tests).
    // The remaining clients (MvcJarUriJwt, Web) are not yet migrated.

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
        var response = await client.GetAsync(".well-known/openid-configuration");
        response.IsSuccessStatusCode.ShouldBeTrue();
    }
}
