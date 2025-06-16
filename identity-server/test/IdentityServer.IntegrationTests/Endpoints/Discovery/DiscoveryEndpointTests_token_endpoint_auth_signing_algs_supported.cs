// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationTests.Endpoints.Discovery;

public class DiscoveryEndpointTests_token_endpoint_auth
{
    private const string Category = "Discovery endpoint - token_endpoint_auth";

    [Theory]
    [Trait("Category", Category)]
    [InlineData(true, true, SecurityAlgorithms.RsaSha256, SecurityAlgorithms.HmacSha256)]
    [InlineData(true, true, SecurityAlgorithms.RsaSsaPssSha384, SecurityAlgorithms.HmacSha384)]
    [InlineData(true, true, SecurityAlgorithms.EcdsaSha512, SecurityAlgorithms.HmacSha512)]
    [InlineData(false, true, SecurityAlgorithms.HmacSha256)]
    [InlineData(false, true, SecurityAlgorithms.HmacSha384)]
    [InlineData(false, true, SecurityAlgorithms.HmacSha512)]
    [InlineData(true, false, SecurityAlgorithms.RsaSha256)]
    [InlineData(true, false, SecurityAlgorithms.RsaSha384)]
    [InlineData(true, false, SecurityAlgorithms.RsaSha512)]
    [InlineData(true, false, SecurityAlgorithms.RsaSsaPssSha256)]
    [InlineData(true, false, SecurityAlgorithms.RsaSsaPssSha384)]
    [InlineData(true, false, SecurityAlgorithms.RsaSsaPssSha512)]
    [InlineData(true, false, SecurityAlgorithms.EcdsaSha256)]
    [InlineData(true, false, SecurityAlgorithms.EcdsaSha384)]
    [InlineData(true, false, SecurityAlgorithms.EcdsaSha512)]

    public async Task token_endpoint_auth_should_match_configuration(bool privateKeyJwtExpected, bool clientSecretJwtExpected, params string[] algorithms)
    {
        // This test verifies that the algorithms supported match the configured algorithms, and that
        // the supported auth methods are appropriate for the algorithms
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += svcs =>
            svcs.AddIdentityServerBuilder().AddJwtBearerClientAuthentication();
        pipeline.Initialize();
        pipeline.Options.SupportedClientAssertionSigningAlgorithms = algorithms;

        var disco = await pipeline.BackChannelClient
            .GetDiscoveryDocumentAsync("https://server/.well-known/openid-configuration");
        disco.IsError.ShouldBeFalse();

        var algorithmsSupported = disco.TokenEndpointAuthenticationSigningAlgorithmsSupported;

        algorithmsSupported.Count().ShouldBe(algorithms.Length);
        algorithmsSupported.ShouldBe(algorithms, ignoreOrder: true);

        var authMethods = disco.TokenEndpointAuthenticationMethodsSupported;
        authMethods.Contains("private_key_jwt").ShouldBe(privateKeyJwtExpected);
        authMethods.Contains("client_secret_jwt").ShouldBe(clientSecretJwtExpected);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task token_endpoint_auth_signing_alg_values_supported_should_default_to_rs_ps_es_hmac()
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

        algorithmsSupported.Count().ShouldBe(12);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSha256);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSha384);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSha512);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSsaPssSha384);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSsaPssSha512);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSsaPssSha256);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.EcdsaSha256);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.EcdsaSha384);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.EcdsaSha512);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.HmacSha256);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.HmacSha384);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.HmacSha512);

        var authMethods = result.TokenEndpointAuthenticationMethodsSupported;
        authMethods.ShouldContain("private_key_jwt");
        authMethods.ShouldContain("client_secret_jwt");
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
