// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationTests.Endpoints.Discovery;

public class DiscoveryEndpointTests_dpop_signing_alg_values_supported
{
    private const string Category = "Discovery endpoint - dpop_signing_alg_values_supported";

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_signing_alg_values_supported_should_match_configuration()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();

        var supportedAlgorithms = new List<string>
        {
            SecurityAlgorithms.RsaSha256,
            SecurityAlgorithms.EcdsaSha256
        };
        pipeline.Options.DPoP.SupportedDPoPSigningAlgorithms = supportedAlgorithms;

        var result =
            await pipeline.BackChannelClient.GetDiscoveryDocumentAsync(
                "https://server/.well-known/openid-configuration");

        var supportedAlgorithmsFromResponse =
            result.TryGetStringArray(OidcConstants.Discovery.DPoPSigningAlgorithmsSupported);
        supportedAlgorithmsFromResponse.ShouldBe(supportedAlgorithms);
    }

    [Fact]
    public async Task dpop_signing_alg_values_supported_should_default_to_rs_ps_es()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();

        var result =
            await pipeline.BackChannelClient.GetDiscoveryDocumentAsync(
                "https://server/.well-known/openid-configuration");
        var algorithmsSupported = result.TryGetStringArray("dpop_signing_alg_values_supported");

        algorithmsSupported.Count().ShouldBe(9);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSha256);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSha384);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSha512);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSsaPssSha384);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSsaPssSha512);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSsaPssSha256);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.EcdsaSha256);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.EcdsaSha384);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.EcdsaSha512);
    }

    [Theory]
    [MemberData(nameof(NullOrEmptySupportedAlgorithms))]
    public async Task dpop_signing_alg_values_supported_should_not_be_present_if_option_is_null_or_empty(ICollection<string> algorithms)
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += svcs =>
            svcs.AddIdentityServerBuilder().AddJwtBearerClientAuthentication();
        pipeline.Initialize();
        pipeline.Options.DPoP.SupportedDPoPSigningAlgorithms = algorithms;

        var result = await pipeline.BackChannelClient
            .GetAsync("https://server/.well-known/openid-configuration");
        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        data.ShouldNotContainKey(OidcConstants.Discovery.DPoPSigningAlgorithmsSupported);
    }

    public static IEnumerable<object[]> NullOrEmptySupportedAlgorithms() =>
        new List<object[]>
        {
            new object[] { Enumerable.Empty<string>() },
            new object[] { null }
        };
}
