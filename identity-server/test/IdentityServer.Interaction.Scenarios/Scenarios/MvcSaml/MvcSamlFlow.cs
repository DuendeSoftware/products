// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Interaction.SharedHosts.SamlClient;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.MvcSaml;

public sealed class MvcSamlFlow : IScenario
{
    private IdentityServerTestHost? _identityServer;
    private SamlClientTestHost? _samlClient;

    public string Name => "MvcSamlFlow";
    public string Description => "MVC app acting as a SAML SP, with IdentityServer as the IdP";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        // Generate a self-signed SP signing certificate
        var spCert = CreateSpSigningCertificate();

        // The SP registration list is created before IS starts so it can be passed
        // to AddInMemorySamlServiceProviders. The list is populated after the SAML
        // client starts and its dynamic port is known (deferred-list pattern).
        var spList = new List<SamlServiceProvider>();

        // 1. Start IdentityServer with SAML enabled
        _identityServer = new IdentityServerTestHost(configurator, "identity-server",
            configureIdentityServer: b => b
                .AddSaml()
                .AddInMemorySamlServiceProviders(spList));

        _identityServer.AddDefaultUsers();

        // SAML needs at minimum openid and profile resources
        _identityServer.SetIdentityResources(
        [
            new IdentityResources.OpenId(),
            new IdentityResources.Profile()
        ]);

        await _identityServer.StartAsync(ct);

        // 2. Start the SAML SP client
        //    Must start AFTER IS so the IdP metadata URL is known
        _samlClient = new SamlClientTestHost(configurator, _identityServer, spCert);
        await _samlClient.StartAsync(ct);

        // 3. Register the SP with IdentityServer using the client's actual dynamic port
        var publicCert = X509CertificateLoader.LoadCertificate(spCert.Export(X509ContentType.Cert));
        spList.Add(new SamlServiceProvider
        {
            EntityId = _samlClient.EntityId,
            DisplayName = "MvcSaml Test Client",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid", "profile" },
            AssertionConsumerServiceUrls =
            [
                new IndexedEndpoint
                {
                    Location = _samlClient.BuildUri("/Saml2/Acs").ToString(),
                    Binding = SamlBinding.HttpPost
                }
            ],
            SingleLogoutServiceUrls =
            [
                new SamlEndpointType
                {
                    Location = _samlClient.BuildUri("/Saml2/Logout").ToString(),
                    Binding = SamlBinding.HttpRedirect
                }
            ],
            SigningBehavior = SamlSigningBehavior.SignAssertion,
            RequireSignedAuthnRequests = true,
            Certificates =
            [
                new ServiceProviderCertificate
                {
                    Certificate = publicCert,
                    Use = KeyUse.Both
                }
            ]
        });

        Links = [_identityServer.Link, _samlClient.Link];
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_samlClient != null)
        {
            await _samlClient.DisposeAsync();
        }

        if (_identityServer != null)
        {
            await _identityServer.DisposeAsync();
        }
    }

    public Command[] GetCommands() => [];

    private static X509Certificate2 CreateSpSigningCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=MvcSaml Test SP Signing Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

        var now = DateTimeOffset.UtcNow;
        var cert = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(10));

        return X509CertificateLoader.LoadPkcs12(
            cert.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    public sealed class Tests(ScenarioFixture<MvcSamlFlow> fixture)
        : PageTest, IClassFixture<ScenarioFixture<MvcSamlFlow>>
    {
        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task Saml_login_flow_shows_claims()
        {
            var clientUrl = fixture.Link("saml-client").ToString();

            // 1. Navigate to the home page
            var response = await Page.GotoAsync(clientUrl);
            response.ShouldNotBeNull();
            response.Ok.ShouldBeTrue();

            // 2. Click "Secure" — should redirect to IdentityServer SAML login
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");

            // 3. Sign in as alice
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

            // 4. Should land on the Secure page showing claims
            await Page.WaitForSelectorAsync("text=Claims");

            var body = await Page.TextContentAsync("body");
            body.ShouldNotBeNull();
            body.ShouldContain("sub");
        }

        [Fact]
        public async Task Saml_logout_ends_session()
        {
            var clientUrl = fixture.Link("saml-client").ToString();

            // Login first
            await Page.GotoAsync(clientUrl);
            await Page.GetByRole(AriaRole.Link, new() { Name = "Secure" }).ClickAsync();
            await Page.WaitForSelectorAsync("input[placeholder='Username']");
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.WaitForSelectorAsync("text=Claims");

            // Logout link should be visible
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).ToBeVisibleAsync();

            // Click Logout — triggers SAML SLO
            await Page.GetByRole(AriaRole.Link, new() { Name = "Logout" }).ClickAsync();

            // IdentityServer may show a logout confirmation
            await Page.WaitForSelectorAsync("text=Logout");
            var yesButton = Page.GetByRole(AriaRole.Button, new() { Name = "Yes" });
            if (await yesButton.IsVisibleAsync())
            {
                await yesButton.ClickAsync();
            }

            await Page.WaitForSelectorAsync("text=logged out");

            // Navigate back — should no longer be authenticated
            await Page.GotoAsync(clientUrl);
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Logout" })).Not.ToBeVisibleAsync();
        }
    }
}
