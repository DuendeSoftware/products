// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Discovery;

public class DiscoveryEndpointTests_token_endpoint_auth_signing_alg_values_supported
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private const string Category = "Discovery endpoint - token_endpoint_auth_signing_alg_values_supported";

    [Fact]
    [Trait("Category", Category)]
    public async Task token_endpoint_auth_signing_alg_values_supported_should_match_configuration()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += svcs =>
            svcs.AddIdentityServerBuilder().AddJwtBearerClientAuthentication();
        pipeline.Initialize();
        pipeline.Options.SupportedClientAssertionSigningAlgorithms =
        [
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.EcdsaSha256
        ];

        var disco = await pipeline.BackChannelClient
            .GetDiscoveryDocumentAsync("https://server/.well-known/openid-configuration");
        disco.IsError.ShouldBeFalse();

        var algorithmsSupported = disco.TokenEndpointAuthenticationSigningAlgorithmsSupported;

        algorithmsSupported.Count().ShouldBe(2);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSsaPssSha256);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.EcdsaSha256);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task token_endpoint_auth_signing_alg_values_supported_should_default_to_rs_ps_es()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += svcs =>
            svcs.AddIdentityServerBuilder().AddJwtBearerClientAuthentication();
        pipeline.Initialize();

        var result =
            await pipeline.BackChannelClient.GetDiscoveryDocumentAsync(
                "https://server/.well-known/openid-configuration");

        result.IsError.ShouldBeFalse();
        var algorithmsSupported = result.TokenEndpointAuthenticationSigningAlgorithmsSupported;

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

    [Fact]
    [Trait("Category", Category)]
    public async Task token_endpoint_auth_signing_alg_values_supported_should_not_be_present_if_private_key_jwt_is_not_configured()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();
        pipeline.Options.SupportedClientAssertionSigningAlgorithms = [SecurityAlgorithms.RsaSha256];

        var disco = await pipeline.BackChannelClient
            .GetDiscoveryDocumentAsync("https://server/.well-known/openid-configuration");

        // Verify assumptions
        disco.IsError.ShouldBeFalse();
        disco.TokenEndpointAuthenticationMethodsSupported.ShouldNotContain("private_key_jwt");
        // we don't even support client_secret_jwt, but per spec, if you DO, you must include the algs supported
        disco.TokenEndpointAuthenticationMethodsSupported.ShouldNotContain("client_secret_jwt");

        // Assert that we got no signing algs.
        disco.TokenEndpointAuthenticationSigningAlgorithmsSupported.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(NullOrEmptySupportedAlgorithms))]
    [Trait("Category", Category)]
    public async Task token_endpoint_auth_signing_alg_values_supported_should_not_be_present_if_option_is_null_or_empty(
        ICollection<string> algorithms)
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += svcs =>
            svcs.AddIdentityServerBuilder().AddJwtBearerClientAuthentication();
        pipeline.Initialize();
        pipeline.Options.SupportedClientAssertionSigningAlgorithms = algorithms;

        var result = await pipeline.BackChannelClient
            .GetAsync("https://server/.well-known/openid-configuration");
        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        data.ShouldNotContainKey(OidcConstants.Discovery.TokenEndpointAuthSigningAlgorithmsSupported);
    }

    public static IEnumerable<object[]> NullOrEmptySupportedAlgorithms() =>
        new List<object[]>
        {
            new object[] { Enumerable.Empty<string>() },
            new object[] { null }
        };
}
