// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Common;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Discovery;

public class DiscoveryEndpointTests_introspection_endpoint_auth_signing_algs_supported : DiscoveryEndpointTestsBase
{
    private const string Category = "Discovery endpoint - introspection_endpoint_auth_signing_alg_values_supported";

    [Fact]
    [Trait("Category", Category)]
    public async Task introspection_endpoint_auth_signing_alg_values_supported_should_match_configuration()
    {
        var pipeline = CreatePipelineWithJwtBearer();
        pipeline.Options.SupportedClientAssertionSigningAlgorithms =
        [
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.EcdsaSha256
        ];
        pipeline.Options.Discovery.ShowIntrospectionEndpointAuthenticationMethods = true;

        var disco = await pipeline.BackChannelClient
            .GetDiscoveryDocumentAsync("https://server/.well-known/openid-configuration");
        disco.IsError.ShouldBeFalse();

        var algorithmsSupported = disco.IntrospectionEndpointAuthenticationSigningAlgorithmsSupported;

        algorithmsSupported.Count().ShouldBe(2);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSsaPssSha256);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.EcdsaSha256);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task introspection_endpoint_auth_signing_alg_values_supported_should_default_to_rs_ps_es()
    {
        var pipeline = CreatePipelineWithJwtBearer();
        pipeline.Options.Discovery.ShowIntrospectionEndpointAuthenticationMethods = true;

        var result =
            await pipeline.BackChannelClient.GetDiscoveryDocumentAsync(
                "https://server/.well-known/openid-configuration");

        result.IsError.ShouldBeFalse();
        var algorithmsSupported = result.IntrospectionEndpointAuthenticationSigningAlgorithmsSupported;

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
    public async Task introspection_endpoint_auth_signing_alg_values_supported_should_not_be_present_if_private_key_jwt_is_not_configured()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();
        pipeline.Options.SupportedClientAssertionSigningAlgorithms = [SecurityAlgorithms.RsaSha256];
        pipeline.Options.Discovery.ShowIntrospectionEndpointAuthenticationMethods = true;

        var disco = await pipeline.BackChannelClient
            .GetDiscoveryDocumentAsync("https://server/.well-known/openid-configuration");

        // Verify assumptions
        disco.IsError.ShouldBeFalse();
        disco.IntrospectionEndpointAuthenticationMethodsSupported.ShouldNotContain("private_key_jwt");
        disco.IntrospectionEndpointAuthenticationMethodsSupported.ShouldNotContain("client_secret_jwt");

        // Assert that we got no signing algs.
        disco.IntrospectionEndpointAuthenticationSigningAlgorithmsSupported.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(NullOrEmptySupportedAlgorithms))]
    [Trait("Category", Category)]
    public async Task introspection_endpoint_auth_signing_alg_values_supported_should_not_be_present_if_option_is_null_or_empty(
        ICollection<string> algorithms)
    {
        var pipeline = CreatePipelineWithJwtBearer();
        pipeline.Options.SupportedClientAssertionSigningAlgorithms = algorithms;
        pipeline.Options.Discovery.ShowIntrospectionEndpointAuthenticationMethods = true;

        var result = await pipeline.BackChannelClient
            .GetAsync("https://server/.well-known/openid-configuration");
        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        data.ShouldNotContainKey(OidcConstants.Discovery.IntrospectionEndpointAuthSigningAlgorithmsSupported);
    }
}

