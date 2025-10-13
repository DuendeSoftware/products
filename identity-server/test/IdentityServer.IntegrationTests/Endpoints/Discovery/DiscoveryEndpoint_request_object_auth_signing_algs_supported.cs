// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Common;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Discovery;

public class DiscoveryEndpoint_request_object_auth_signing_algs_supported_Tests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private const string Category = "Discovery endpoint - request_object_signing_algs_supported";

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_signing_alg_values_supported_should_match_configuration()
    {
        var pipeline = new IdentityServerPipeline();

        pipeline.Initialize();
        pipeline.Options.SupportedRequestObjectSigningAlgorithms =
        [
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.EcdsaSha256
        ];

        var result = await pipeline.BackChannelClient
            .GetDiscoveryDocumentAsync("https://server/.well-known/openid-configuration", _ct);
        var algorithmsSupported = result.TryGetStringArray("request_object_signing_alg_values_supported");

        algorithmsSupported.Count().ShouldBe(2);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSsaPssSha256);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.EcdsaSha256);
    }

    [Theory]
    [MemberData(nameof(NullOrEmptySupportedAlgorithms))]
    [Trait("Category", Category)]
    public async Task request_object_signing_alg_values_supported_should_not_be_present_if_option_is_null_or_empty(
        ICollection<string> algorithms)
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();
        pipeline.Options.SupportedRequestObjectSigningAlgorithms = algorithms;

        var result = await pipeline.BackChannelClient
            .GetAsync("https://server/.well-known/openid-configuration", _ct);
        var json = await result.Content.ReadAsStringAsync(_ct);
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        data.ShouldNotContainKey("request_object_signing_alg_values_supported");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_object_signing_alg_values_supported_should_default_to_rs_ps_es()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();

        var result =
            await pipeline.BackChannelClient.GetDiscoveryDocumentAsync(
                "https://server/.well-known/openid-configuration", _ct);
        var algorithmsSupported = result.TryGetStringArray("request_object_signing_alg_values_supported");

        algorithmsSupported.ShouldBe([
            SecurityAlgorithms.RsaSha256,
            SecurityAlgorithms.RsaSha384,
            SecurityAlgorithms.RsaSha512,
            SecurityAlgorithms.RsaSsaPssSha384,
            SecurityAlgorithms.RsaSsaPssSha512,
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.EcdsaSha256,
            SecurityAlgorithms.EcdsaSha384,
            SecurityAlgorithms.EcdsaSha512,
        ], ignoreOrder: true);
    }

    public static IEnumerable<object[]> NullOrEmptySupportedAlgorithms() =>
        new List<object[]>
        {
            new object[] { Enumerable.Empty<string>() },
            new object[] { null }
        };
}
