// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityServer.IntegrationTests.Endpoints.Saml;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Licensing;

public class LicensingTests(WebServerFixture webApp)
{
    [Fact]
    public async Task Can_signin_without_any_license()
    {
        await using var fixture = new LicensingFixture(webApp);

        await fixture.InitializeAsync();

        var httpClient = fixture.Client.CreateClient(true);
        var result = await httpClient.GetAsync("/login");

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await result.Content.ReadAsStringAsync();
        content.ShouldContain("Ok");
    }

    [Fact]
    public async Task Can_signin_with_all_skus()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.Licenses = TestLicense.GetAllSkus().ToList();
        await fixture.InitializeAsync();

        var httpClient = fixture.Client.CreateClient(true);
        var result = await httpClient.GetAsync("/login");

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await result.Content.ReadAsStringAsync();
        content.ShouldContain("Ok");
    }

    [Fact]
    public async Task Saml_idp_requires_entitlement() =>
        await ShouldThrowWithoutEntitlementAsync(
            fixture => { fixture.ConfigureIdentityServer += idsrv => idsrv.AddSaml(); }, "PTC-010");

    [Fact]
    public async Task Key_management_requires_entitlement() => await ShouldThrowWithoutEntitlementAsync(
        fixture => { fixture.ConfigureIdentityServerOptions += options => options.KeyManagement.Enabled = true; },
        "PLT-004");

    [Fact]
    public async Task Server_side_sessions_requires_entitlement() => await ShouldThrowWithoutEntitlementAsync(
        fixture => { fixture.ConfigureIdentityServer += idsrv => idsrv.AddServerSideSessions(); }, "PLT-021");

    [Fact]
    public async Task Dynamic_providers_requires_entitlement() => await ShouldThrowWithoutEntitlementAsync(fixture =>
    {
        fixture.ConfigureIdentityServer += idsrv => idsrv.AddInMemoryIdentityProviders(
        [
            new OidcProvider
            {
                Scheme = "test-oidc",
                Authority = "https://example.com",
                ClientId = "test"
            }
        ]);
    }, "PLT-005");

    [Fact]
    public async Task Par_returns_not_found_without_entitlement()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.Licenses = TestLicense.GetAllSkus()
            .Except(["PTC-004"])
            .ToList();
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var result = await client.PostAsync("/connect/par", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "client"),
            new KeyValuePair<string, string>("client_secret", "secret"),
            new KeyValuePair<string, string>("response_type", "code"),
            new KeyValuePair<string, string>("scope", "openid"),
            new KeyValuePair<string, string>("redirect_uri", fixture.Client.BuildUrl("/signin-oidc").ToString())
        ]));

        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Par_succeeds_with_entitlement()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.Licenses = TestLicense.GetAllSkus().ToList();
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var result = await client.PostAsync("/connect/par", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "client"),
            new KeyValuePair<string, string>("client_secret", "secret"),
            new KeyValuePair<string, string>("response_type", "code"),
            new KeyValuePair<string, string>("scope", "openid"),
            new KeyValuePair<string, string>("redirect_uri", fixture.Client.BuildUrl("/signin-oidc").ToString())
        ]));

        result.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Par_succeeds_without_any_license()
    {
        await using var fixture = new LicensingFixture(webApp);
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var result = await client.PostAsync("/connect/par", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "client"),
            new KeyValuePair<string, string>("client_secret", "secret"),
            new KeyValuePair<string, string>("response_type", "code"),
            new KeyValuePair<string, string>("scope", "openid"),
            new KeyValuePair<string, string>("redirect_uri", fixture.Client.BuildUrl("/signin-oidc").ToString())
        ]));

        result.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DPoP_throws_without_entitlement()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.Licenses = TestLicense.GetAllSkus()
            .Except(["PTC-006"])
            .ToList();
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token");
        request.Content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "client"),
            new KeyValuePair<string, string>("client_secret", "secret"),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", "fake_code"),
            new KeyValuePair<string, string>("redirect_uri", fixture.Client.BuildUrl("/signin-oidc").ToString())
        ]);
        // Adding a DPoP header triggers the license check
        request.Headers.Add("DPoP", "fake-dpop-proof");

        var result = await client.SendAsync(request);

        // DPoP without entitlement throws, which results in a 500
        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task DPoP_succeeds_with_entitlement()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.Licenses = TestLicense.GetAllSkus().ToList();
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token");
        request.Content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "client"),
            new KeyValuePair<string, string>("client_secret", "secret"),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", "fake_code"),
            new KeyValuePair<string, string>("redirect_uri", fixture.Client.BuildUrl("/signin-oidc").ToString())
        ]);
        request.Headers.Add("DPoP", "fake-dpop-proof");

        var result = await client.SendAsync(request);

        // Should not be 500 — might be 400 for invalid DPoP proof, but not an entitlement error
        result.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task DPoP_does_not_throw_without_any_license()
    {
        await using var fixture = new LicensingFixture(webApp);
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token");
        request.Content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "client"),
            new KeyValuePair<string, string>("client_secret", "secret"),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", "fake_code"),
            new KeyValuePair<string, string>("redirect_uri", fixture.Client.BuildUrl("/signin-oidc").ToString())
        ]);
        request.Headers.Add("DPoP", "fake-dpop-proof");

        var result = await client.SendAsync(request);

        // No license = no enforcement
        result.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Saml_sp_returns_not_found_without_entitlement()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.ConfigureServices = services =>
        {
            services.AddAuthentication().AddSamlServiceProvider(options =>
            {
                options.SpEntityId = "https://sp.example.com";
                options.IdpEntityId = "https://idp.example.com";
                options.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            });
        };
        fixture.Licenses = TestLicense.GetAllSkus()
            .Except(["PTC-011"])
            .ToList();
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var result = await client.GetAsync("/Saml2");

        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Saml_sp_succeeds_with_entitlement()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.ConfigureServices = services =>
        {
            services.AddAuthentication().AddSamlServiceProvider(options =>
            {
                options.SpEntityId = "https://sp.example.com";
                options.IdpEntityId = "https://idp.example.com";
                options.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            });
        };
        fixture.Licenses = TestLicense.GetAllSkus().ToList();
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var result = await client.GetAsync("/Saml2");

        result.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Saml_sp_succeeds_without_any_license()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.ConfigureServices = services =>
        {
            services.AddAuthentication().AddSamlServiceProvider(options =>
            {
                options.SpEntityId = "https://sp.example.com";
                options.IdpEntityId = "https://idp.example.com";
                options.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            });
        };
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var result = await client.GetAsync("/Saml2");

        result.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ciba_does_not_fail_without_entitlement()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.Clients =
        [
            new Client
            {
                ClientId = "ciba-client",
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedGrantTypes = GrantTypes.Ciba,
                AllowedScopes = { "openid" }
            }
        ];
        fixture.Licenses = TestLicense.GetAllSkus()
            .Except(["PTC-022"])
            .ToList();
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var result = await client.PostAsync("/connect/ciba", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "ciba-client"),
            new KeyValuePair<string, string>("client_secret", "secret"),
            new KeyValuePair<string, string>("scope", "openid"),
            new KeyValuePair<string, string>("login_hint", "user@example.com")
        ]));

        // CIBA without entitlement logs a warning but does NOT block the request
        result.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
        result.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resource_isolation_does_not_fail_without_entitlement()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.Licenses = TestLicense.GetAllSkus()
            .Except(["IS-001"])
            .ToList();
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var result = await client.GetAsync(
            "/connect/authorize?client_id=client&response_type=code&scope=openid" +
            $"&redirect_uri={Uri.EscapeDataString(fixture.Client.BuildUrl("/signin-oidc").ToString())}" +
            "&resource=https://api.example.com");

        // Resource isolation without entitlement silently clears indicators — does NOT block
        result.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
        result.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Client_count_validation_does_not_block_requests()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.Clients.Add(new Client
        {
            ClientId = "extra-client",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RedirectUris = { fixture.Client.BuildUrl("/signin-oidc").ToString() },
            AllowedScopes = { "openid" }
        });
        fixture.Licenses = TestLicense.GetAllSkus()
            .Except(["PTC-009"])
            .ToList();
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();

        // Make requests with two different client IDs to exercise count tracking
        var result1 = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "client"),
            new KeyValuePair<string, string>("client_secret", "secret"),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", "fake"),
            new KeyValuePair<string, string>("redirect_uri", fixture.Client.BuildUrl("/signin-oidc").ToString())
        ]));

        var result2 = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "extra-client"),
            new KeyValuePair<string, string>("client_secret", "secret"),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", "fake"),
            new KeyValuePair<string, string>("redirect_uri", fixture.Client.BuildUrl("/signin-oidc").ToString())
        ]));

        // Client count validation only logs — never blocks
        result1.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
        result2.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Issuer_count_validation_does_not_block_requests()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.Licenses = TestLicense.GetAllSkus()
            .Except(["PLT-020"])
            .ToList();
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();
        var result = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", "client"),
            new KeyValuePair<string, string>("client_secret", "secret"),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", "fake"),
            new KeyValuePair<string, string>("redirect_uri", fixture.Client.BuildUrl("/signin-oidc").ToString())
        ]));

        // Issuer count validation only logs — never blocks
        result.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Saml_idp_count_validation_does_not_block_requests()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.ConfigureServices = services =>
        {
            services.AddAuthentication().AddSamlServiceProvider(options =>
            {
                options.SpEntityId = "https://sp.example.com";
                options.IdpEntityId = "https://idp.example.com";
                options.SingleSignOnServiceUrl = "https://idp.example.com/sso";
            });
        };
        fixture.Licenses = TestLicense.GetAllSkus()
            .Except(["PTC-014"])
            .ToList();
        await fixture.InitializeAsync();

        // The IdP count validation fires when SAML SP options are configured at startup.
        // If we got here without throwing, it means count validation only logged.
        var client = fixture.IdentityServer.CreateClient();
        var result = await client.GetAsync("/Saml2");

        result.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Saml_sp_count_validation_does_not_block_requests()
    {
        await using var fixture = new LicensingFixture(webApp);
        fixture.ConfigureIdentityServer += idsrv =>
        {
            idsrv.AddSaml();
            idsrv.AddInMemorySamlServiceProviders(
            [
                new SamlServiceProvider
                {
                    EntityId = "https://sp1.example.com",
                    AllowedScopes = { "openid" },
                    AssertionConsumerServiceUrls =
                    [
                        new IndexedEndpoint { Location = "https://sp1.example.com/acs", Binding = SamlBinding.HttpPost }
                    ]
                },
                new SamlServiceProvider
                {
                    EntityId = "https://sp2.example.com",
                    AllowedScopes = { "openid" },
                    AssertionConsumerServiceUrls =
                    [
                        new IndexedEndpoint { Location = "https://sp2.example.com/acs", Binding = SamlBinding.HttpPost }
                    ]
                }
            ]);
        };
        fixture.Licenses = TestLicense.GetAllSkus()
            .Except(["PTC-013"])
            .ToList();
        await fixture.InitializeAsync();

        var client = fixture.IdentityServer.CreateClient();

        // Send AuthnRequests from two different SPs to exercise count tracking
        var authnRequest1 = BuildAuthnRequest("https://sp1.example.com", fixture.IdentityServer.BaseAddress);
        var encoded1 = await SamlTestHelpers.EncodeRequest(authnRequest1);
        var result1 = await client.GetAsync($"/Saml2/SSO?SAMLRequest={encoded1}");

        var authnRequest2 = BuildAuthnRequest("https://sp2.example.com", fixture.IdentityServer.BaseAddress);
        var encoded2 = await SamlTestHelpers.EncodeRequest(authnRequest2);
        var result2 = await client.GetAsync($"/Saml2/SSO?SAMLRequest={encoded2}");

        // SP count validation only logs — never blocks
        result1.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
        result2.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
    }

    private static string BuildAuthnRequest(string issuerEntityId, Uri destination) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <samlp:AuthnRequest
             ID="_test_{Guid.NewGuid():N}"
             Version="2.0"
             IssueInstant="{DateTime.UtcNow:O}"
             Destination="{destination}Saml2/SSO"
             ProtocolBinding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"
             AssertionConsumerServiceURL="https://sp.example.com/acs"
             xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
             xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
             <saml:Issuer>{issuerEntityId}</saml:Issuer>
         </samlp:AuthnRequest>
         """;

    private async Task ShouldThrowWithoutEntitlementAsync(Action<LicensingFixture> configureFixture, string skuToOmit)
    {
        await using var throwingFixture = new LicensingFixture(webApp);
        configureFixture(throwingFixture);
        throwingFixture.Licenses = TestLicense.GetAllSkus()
            .Except([skuToOmit])
            .ToList();
        await Should.ThrowAsync<Exception>(async () => { await throwingFixture.InitializeAsync(); },
            $"Without entitlement {skuToOmit} startup should fail");

        await using var withAllSkus = new LicensingFixture(webApp);
        configureFixture(withAllSkus);
        withAllSkus.Licenses = TestLicense.GetAllSkus().ToList();
        await withAllSkus.InitializeAsync();

        await using var withoutLicense = new LicensingFixture(webApp);
        configureFixture(withoutLicense);
        await withoutLicense.InitializeAsync();
    }
}
