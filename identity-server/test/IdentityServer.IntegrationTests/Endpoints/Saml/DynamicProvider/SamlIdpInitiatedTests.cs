// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;

public class SamlIdpInitiatedTests(ITestOutputHelper output)
{
    private const string Category = "IDP-Initiated SAML SSO";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task idp_initiated_sso_succeeds_and_surfaces_user_identity()
    {
        // Arrange
        await using var fixture = new SamlDynamicProviderFixture(output);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();

        // Sign in the test user at the IdP
        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);

        // Act - trigger IDP-initiated SSO (no relay state)
        var response = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}");

        // Assert - should end up at the external callback with user info
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(_ct);
        body.ShouldContain(SamlDynamicProviderFixture.TestUserSub);
        body.ShouldContain("saml-idp"); // provider scheme name
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task idp_initiated_sso_surfaces_relay_state()
    {
        // Arrange
        await using var fixture = new SamlDynamicProviderFixture(output);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();

        // Sign in the test user at the IdP
        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);

        // Act - trigger IDP-initiated SSO with relay state
        var relayState = "/my-dashboard";
        var response = await fixture.FollowRedirectChainAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}&relayState={Uri.EscapeDataString(relayState)}");

        // Assert - relay state should be surfaced in the response
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(_ct);
        body.ShouldContain(SamlDynamicProviderFixture.TestUserSub);
        // RelayState may be URL-encoded during form submission
        body.ShouldContain("my-dashboard");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task idp_initiated_sso_rejected_when_not_allowed()
    {
        // Arrange
        await using var fixture = new SamlDynamicProviderFixture(output);
        await fixture.InitializeAsync();

        var idpUri = fixture.IdpHost!.Uri();
        var spUri = fixture.SpHost!.Uri();

        // Disable AllowIdpInitiated on the SP registration at the IdP
        await using var scope = fixture.IdpHost.ConfiguredServices.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Duende.IdentityServer.EntityFramework.DbContexts.ConfigurationDbContext>();
        var sp = await db.SamlServiceProviders.FirstAsync(s => s.EntityId == spUri, _ct);
        sp.AllowIdpInitiated = false;
        await db.SaveChangesAsync(_ct);

        // Sign in the test user at the IdP
        await fixture.BrowserClient!.GetAsync($"{idpUri}/auto-login", _ct);

        // Act - try to trigger IDP-initiated SSO
        var response = await fixture.BrowserClient.GetAsync(
            $"{idpUri}/idp-initiated-sso?sp={Uri.EscapeDataString(spUri)}", _ct);

        // Assert - should be rejected (400 error from our endpoint)
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
