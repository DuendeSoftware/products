// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Interaction.SharedHosts.MvcClient;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Shouldly;

namespace Duende.IdentityServer.Interaction.Scenarios.SamlSp;

public sealed class SamlSpPostBindingFlow : IScenario
{
    private IdentityServerTestHost? _upstreamIdentityProvider;
    private IdentityServerTestHost? _middleServiceProvider;
    private ClientWebAppTestHost? _client;

    public string Name => "SamlSpPostBindingFlow";
    public string Description => "OIDC client using IdentityServer as a SAML SP with outbound HTTP-POST binding";
    public IReadOnlyList<ScenarioLink> Links { get; private set; } = [];

    public async Task StartAsync(IScenarioConfigurator configurator, CancellationToken ct)
    {
        var upstreamSigningCertificate = CreateSigningCertificate();
        var serviceProviders = new List<SamlServiceProvider>();

        _upstreamIdentityProvider = new IdentityServerTestHost(
            configurator,
            "saml-upstream-idp",
            identityServer => identityServer
                .AddSigningCredential(upstreamSigningCertificate)
                .AddSaml()
                .AddInMemorySamlServiceProviders(serviceProviders));
        _upstreamIdentityProvider.AddDefaultUsers();
        _upstreamIdentityProvider.SetIdentityResources(
        [
            new IdentityResources.OpenId(),
            new IdentityResources.Profile()
        ]);
        await _upstreamIdentityProvider.StartAsync(ct);

        var providers = new List<SamlProvider>
        {
            new()
            {
                Scheme = "saml-idp",
                DisplayName = "Upstream SAML IdP",
                Enabled = true,
                IdpEntityId = _upstreamIdentityProvider.BuildUri("/Saml2").ToString(),
                SingleSignOnServiceUrl = _upstreamIdentityProvider.BuildUri("/Saml2/SSO").ToString(),
                SigningCertificateBase64 = Convert.ToBase64String(
                    upstreamSigningCertificate.Export(X509ContentType.Cert)),
                BindingType = "post",
                WantAssertionsSigned = false
            }
        };

        _middleServiceProvider = new IdentityServerTestHost(
            configurator,
            "saml-middle-sp",
            identityServer => identityServer
                .AddSamlDynamicProvider()
                .AddInMemorySamlProviders(providers));
        _middleServiceProvider.AddDefaultUsers();
        _middleServiceProvider.SetIdentityResources(
        [
            new IdentityResources.OpenId(),
            new IdentityResources.Profile()
        ]);
        await _middleServiceProvider.StartAsync(ct);

        serviceProviders.Add(new SamlServiceProvider
        {
            EntityId = _middleServiceProvider.BuildUri().ToString().TrimEnd('/'),
            DisplayName = "Middle IdentityServer SAML SP",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid", "profile" },
            AssertionConsumerServiceUrls =
            [
                new IndexedEndpoint
                {
                    Location = _middleServiceProvider.BuildUri("/federation/saml-idp/Saml2/Acs").ToString(),
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            SigningBehavior = SamlSigningBehavior.SignAssertion,
            RequireSignedAuthnRequests = false
        });

        _client = new ClientWebAppTestHost(
            configurator,
            _middleServiceProvider,
            apiHost: null,
            name: "saml-post-client",
            configureOpenIdConnect: options =>
            {
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
            });
        await _client.StartAsync(ct);

        _middleServiceProvider.AddClient(_client, client =>
        {
            client.ClientId = _client.Name;
            client.ClientName = "SAML POST binding browser client";
            client.EnableLocalLogin = false;
            client.RequireConsent = false;
            client.AllowedGrantTypes = GrantTypes.Code;
            client.AllowedScopes = ["openid", "profile"];
        });

        Links =
        [
            new ScenarioLink("Start SAML POST sign-in", _client.BuildUri("/Secure")),
            new ScenarioLink("OIDC client home", _client.BuildUri()),
            new ScenarioLink("Middle IdentityServer SAML SP", _middleServiceProvider.BuildUri()),
            new ScenarioLink("Upstream IdentityServer SAML IdP", _upstreamIdentityProvider.BuildUri())
        ];
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_client != null)
        {
            await _client.DisposeAsync();
        }

        if (_middleServiceProvider != null)
        {
            await _middleServiceProvider.DisposeAsync();
        }

        if (_upstreamIdentityProvider != null)
        {
            await _upstreamIdentityProvider.DisposeAsync();
        }
    }

    public Command[] GetCommands() => [];

    private static X509Certificate2 CreateSigningCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=SAML POST Binding Upstream IdP",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx),
            null,
            X509KeyStorageFlags.Exportable);
    }

    public sealed class Tests(ScenarioFixture<SamlSpPostBindingFlow> fixture)
        : PageTest, IClassFixture<ScenarioFixture<SamlSpPostBindingFlow>>
    {
        public override BrowserNewContextOptions ContextOptions() => new()
        {
            IgnoreHTTPSErrors = true
        };

        [Fact]
        public async Task Saml_POST_login_flow_shows_claims()
        {
            const string expectedScriptHash = "sha256-IQKtK10TFgRroV/L1+sRadhw5yAEkHE3GlbgJgxr7K4=";
            var upstreamSsoUrl = fixture.Link("Upstream IdentityServer SAML IdP").ToString().TrimEnd('/') + "/Saml2/SSO";
            var cspViolation = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var upstreamSsoRequested = false;

            Page.Console += (_, message) =>
            {
                if (message.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase)
                    && message.Text.Contains(expectedScriptHash, StringComparison.Ordinal))
                {
                    cspViolation.TrySetResult(message.Text);
                }
            };
            Page.Request += (_, request) =>
            {
                if (request.Url.Equals(upstreamSsoUrl, StringComparison.OrdinalIgnoreCase))
                {
                    upstreamSsoRequested = true;
                }
            };

            await Page.GotoAsync(fixture.Link("Start SAML POST sign-in").ToString());

            var upstreamLogin = Page.GetByPlaceholder("Username").WaitForAsync(new() { Timeout = 10_000 });
            var firstResult = await Task.WhenAny(upstreamLogin, cspViolation.Task);
            if (firstResult == cspViolation.Task)
            {
                var relayForm = Page.Locator("form[name='samlPostBindingSubmit']");
                await Expect(relayForm).ToHaveCountAsync(1);
                (await relayForm.GetAttributeAsync("action")).ShouldBe(upstreamSsoUrl);
                await Expect(relayForm.Locator("input[name='SAMLRequest']")).ToHaveCountAsync(1);
                upstreamSsoRequested.ShouldBeFalse();

                throw new InvalidOperationException(
                    $"The browser remained on the SAML POST relay page at {Page.Url} because CSP blocked " +
                    $"document.forms.samlPostBindingSubmit.submit(). " +
                    $"No request reached the upstream IdP SSO endpoint. Browser console: {await cspViolation.Task}");
            }

            await upstreamLogin;
            await Page.GetByPlaceholder("Username").FillAsync("alice");
            await Page.GetByPlaceholder("Password").FillAsync("alice");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

            await Page.WaitForSelectorAsync("text=Claims");
            var body = await Page.TextContentAsync("body");
            body.ShouldNotBeNull();
            body.ShouldContain("sub");
        }
    }
}
