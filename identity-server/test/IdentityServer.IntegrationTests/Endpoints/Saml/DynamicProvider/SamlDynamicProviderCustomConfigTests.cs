// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;

public class SamlDynamicProviderCustomConfigTests(ITestOutputHelper output)
{

    [Fact]
    [Trait("Category", "Dynamic SAML provider")]
    public async Task CustomConfigureOptionsIsInvokedDuringDynamicProviderSetup()
    {
        // Arrange
        var tracker = new InvocationTracker();

        await using var fixture = new SamlDynamicProviderFixture(output,
            additionalSpServices: services =>
            {
                services.AddSingleton(tracker);
                services.ConfigureOptions<TestSamlConfigureOptions>();
            });
        await fixture.InitializeAsync();

        var clientUri = fixture.ClientHost!.Uri();

        // Act — trigger the full flow which instantiates the dynamic SAML provider
        var response = await fixture.FollowRedirectChainAsync($"{clientUri}/protected");

        // Assert — the flow completed successfully AND our custom configure was invoked
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        tracker.InvokedForSchemes.ShouldContain("saml-idp");

        // Assert — the customer override actually propagated (OutboundSigningAlgorithm was customized)
        tracker.ConfiguredAlgorithms.ShouldContain("http://www.w3.org/2001/04/xmldsig-more#rsa-sha384");
    }

    [Fact]
    [Trait("Category", "Dynamic SAML provider")]
    public async Task BaselinePrePopulationFromSamlProviderIsVisibleToCustomerConfigure()
    {
        // Arrange
        var tracker = new InvocationTracker();

        await using var fixture = new SamlDynamicProviderFixture(output,
            additionalSpServices: services =>
            {
                services.AddSingleton(tracker);
                services.ConfigureOptions<BaselineAssertingConfigureOptions>();
            });
        await fixture.InitializeAsync();

        var clientUri = fixture.ClientHost!.Uri();

        // Act — trigger the full flow which instantiates the dynamic SAML provider
        var response = await fixture.FollowRedirectChainAsync($"{clientUri}/protected");

        // Assert — the flow completed and baseline values from SamlProvider were pre-populated
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        tracker.InvokedForSchemes.ShouldContain("saml-idp");

        // The fixture seeds WantAssertionsSigned = false on the SamlProvider,
        // so the baseline SamlAuthenticationConfigureOptions should have set it before our callback ran.
        tracker.BaselineWantAssertionsSigned.ShouldContain(false);

        // SignInScheme should be pre-populated from DynamicProviderOptions
        tracker.BaselineSignInSchemes.ShouldAllBe(s => !string.IsNullOrWhiteSpace(s));
    }

    /// <summary>
    /// Tracks invocations across the test without static state.
    /// </summary>
    internal sealed class InvocationTracker
    {
        public List<string> InvokedForSchemes { get; } = [];
        public List<string?> ConfiguredAlgorithms { get; } = [];
        public List<bool?> BaselineWantAssertionsSigned { get; } = [];
        public List<string?> BaselineSignInSchemes { get; } = [];
    }

    /// <summary>
    /// Test implementation of ConfigureAuthenticationOptions that records invocations
    /// and overrides OutboundSigningAlgorithm to prove the value propagates without
    /// breaking the SAML flow (unlike SpEntityId which must match the IdP's registered SP).
    /// </summary>
    private sealed class TestSamlConfigureOptions : ConfigureAuthenticationOptions<SamlAuthenticationOptions, SamlProvider>
    {
        private readonly InvocationTracker _tracker;

        public TestSamlConfigureOptions(
            InvocationTracker tracker,
            Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
            Microsoft.Extensions.Logging.ILogger<TestSamlConfigureOptions> logger)
            : base(httpContextAccessor, logger) =>
            _tracker = tracker;

        protected override void Configure(ConfigureAuthenticationContext<SamlAuthenticationOptions, SamlProvider> context)
        {
            _tracker.InvokedForSchemes.Add(context.IdentityProvider.Scheme);
            context.AuthenticationOptions.OutboundSigningAlgorithm = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha384";
            _tracker.ConfiguredAlgorithms.Add(context.AuthenticationOptions.OutboundSigningAlgorithm);
        }
    }

    /// <summary>
    /// Asserts that baseline values from SamlAuthenticationConfigureOptions are already
    /// present when the customer's configure callback runs.
    /// </summary>
    private sealed class BaselineAssertingConfigureOptions : ConfigureAuthenticationOptions<SamlAuthenticationOptions, SamlProvider>
    {
        private readonly InvocationTracker _tracker;

        public BaselineAssertingConfigureOptions(
            InvocationTracker tracker,
            Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
            Microsoft.Extensions.Logging.ILogger<BaselineAssertingConfigureOptions> logger)
            : base(httpContextAccessor, logger) =>
            _tracker = tracker;

        protected override void Configure(ConfigureAuthenticationContext<SamlAuthenticationOptions, SamlProvider> context)
        {
            _tracker.InvokedForSchemes.Add(context.IdentityProvider.Scheme);
            _tracker.BaselineWantAssertionsSigned.Add(context.AuthenticationOptions.WantAssertionsSigned);
            _tracker.BaselineSignInSchemes.Add(context.AuthenticationOptions.SignInScheme);
        }
    }
}
