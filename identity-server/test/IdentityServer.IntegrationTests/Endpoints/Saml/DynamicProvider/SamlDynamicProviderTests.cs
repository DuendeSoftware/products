// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;

public class SamlDynamicProviderTests(ITestOutputHelper output)
{
    private const string Category = "Dynamic SAML provider";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task end_to_end_dynamic_saml_provider_federation_flow()
    {
        // Arrange
        await using var fixture = new SamlDynamicProviderFixture(output);
        await fixture.InitializeAsync();

        var clientUri = fixture.ClientHost!.Uri();

        // Act — hit the protected endpoint on Webapp 1.
        // The redirect chain is:
        //   Webapp 1 /protected → OIDC challenge → Webapp 2 authorize
        //   → dynamic SAML challenge → Webapp 3 /Saml2/SSO
        //   → unauthenticated → /auto-login → signs in test user → back to SAML callback
        //   → SAML response POST → Webapp 2 ACS → OIDC callback → Webapp 1 /protected
        var response = await fixture.FollowRedirectChainAsync($"{clientUri}/protected");

        // Assert — the final response should be 200 OK with the test user's claims
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(_ct);
        body.ShouldContain(SamlDynamicProviderFixture.TestUserSub);
    }
}
