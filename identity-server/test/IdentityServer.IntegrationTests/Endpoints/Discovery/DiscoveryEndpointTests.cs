// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Models;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace IntegrationTests.Endpoints.Discovery;

public class DiscoveryEndpointTests
{
    private const string Category = "Discovery endpoint";

    [Fact]
    [Trait("Category", Category)]
    public async Task Issuer_uri_should_be_lowercase()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize("/ROOT");

        var result = await pipeline.BackChannelClient.GetAsync("HTTPS://SERVER/ROOT/.WELL-KNOWN/OPENID-CONFIGURATION");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().ShouldBe("https://server/root");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_lower_case_issuer_option_disabled_issuer_uri_should_be_preserved()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize("/ROOT");

        pipeline.Options.LowerCaseIssuerUri = false;

        var result = await pipeline.BackChannelClient.GetAsync("HTTPS://SERVER/ROOT/.WELL-KNOWN/OPENID-CONFIGURATION");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().ShouldBe("https://server/ROOT");
    }

    private void Pipeline_OnPostConfigureServices(IServiceCollection obj) => throw new System.NotImplementedException();

    [Fact]
    [Trait("Category", Category)]
    public async Task Algorithms_supported_should_match_signing_key()
    {
        var key = CryptoHelper.CreateECDsaSecurityKey(JsonWebKeyECTypes.P256);
        var expectedAlgorithm = SecurityAlgorithms.EcdsaSha256;

        var pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += services =>
        {
            // add key to standard RSA key
            services.AddIdentityServerBuilder()
                .AddSigningCredential(key, expectedAlgorithm);
        };
        pipeline.Initialize("/ROOT");

        var result = await pipeline.BackChannelClient.GetAsync("https://server/root/.well-known/openid-configuration");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        var algorithmsSupported = data["id_token_signing_alg_values_supported"].EnumerateArray()
            .Select(x => x.GetString()).ToList();

        algorithmsSupported.Count.ShouldBe(2);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.RsaSha256);
        algorithmsSupported.ShouldContain(SecurityAlgorithms.EcdsaSha256);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Jwks_entries_should_countain_crv()
    {
        var ecdsaKey = CryptoHelper.CreateECDsaSecurityKey(JsonWebKeyECTypes.P256);
        var parameters = ecdsaKey.ECDsa.ExportParameters(true);

        var pipeline = new IdentityServerPipeline();

        var jsonWebKeyFromECDsa = new JsonWebKey()
        {
            Kty = JsonWebAlgorithmsKeyTypes.EllipticCurve,
            Use = "sig",
            Kid = ecdsaKey.KeyId,
            KeyId = ecdsaKey.KeyId,
            X = Base64UrlEncoder.Encode(parameters.Q.X),
            Y = Base64UrlEncoder.Encode(parameters.Q.Y),
            D = Base64UrlEncoder.Encode(parameters.D),
            Crv = JsonWebKeyECTypes.P256,
            Alg = SecurityAlgorithms.EcdsaSha256
        };
        pipeline.OnPostConfigureServices += services =>
        {
            // add ECDsa as JsonWebKey
            services.AddIdentityServerBuilder()
                .AddSigningCredential(jsonWebKeyFromECDsa, SecurityAlgorithms.EcdsaSha256);
        };

        pipeline.Initialize("/ROOT");

        var result = await pipeline.BackChannelClient.GetAsync("https://server/root/.well-known/openid-configuration/jwks");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        var keys = data["keys"].EnumerateArray().ToList();
        keys.Count.ShouldBe(2);

        var key = keys[1];
        var crv = key.TryGetValue("crv");
        crv.GetString().ShouldBe(JsonWebKeyECTypes.P256);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Jwks_entries_should_contain_alg()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize("/ROOT");

        var result = await pipeline.BackChannelClient.GetAsync("https://server/root/.well-known/openid-configuration/jwks");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        var keys = data["keys"];
        var key = keys[0];

        var alg = key.TryGetValue("alg");
        alg.GetString().ShouldBe(Constants.SigningAlgorithms.RSA_SHA_256);
    }

    [Theory]
    [InlineData(JsonWebKeyECTypes.P256, SecurityAlgorithms.EcdsaSha256)]
    [InlineData(JsonWebKeyECTypes.P384, SecurityAlgorithms.EcdsaSha384)]
    [InlineData(JsonWebKeyECTypes.P521, SecurityAlgorithms.EcdsaSha512)]
    [Trait("Category", Category)]
    public async Task Jwks_with_ecdsa_should_have_parsable_key(string crv, string alg)
    {
        var key = CryptoHelper.CreateECDsaSecurityKey(crv);

        var pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += services =>
        {
            services.AddIdentityServerBuilder()
                .AddSigningCredential(key, alg);
        };
        pipeline.Initialize("/ROOT");

        var result = await pipeline.BackChannelClient.GetAsync("https://server/root/.well-known/openid-configuration/jwks");

        var json = await result.Content.ReadAsStringAsync();
        var jwks = new JsonWebKeySet(json);
        var parsedKeys = jwks.GetSigningKeys();

        var matchingKey = parsedKeys.FirstOrDefault(x => x.KeyId == key.KeyId);
        matchingKey.ShouldNotBeNull();
        matchingKey.ShouldBeOfType<ECDsaSecurityKey>();
    }

    [Fact]
    public async Task Jwks_with_two_key_using_different_algs_expect_different_alg_values()
    {
        var ecdsaKey = CryptoHelper.CreateECDsaSecurityKey();
        var rsaKey = CryptoHelper.CreateRsaSecurityKey();

        var pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += services =>
        {
            services.AddIdentityServerBuilder()
                .AddSigningCredential(ecdsaKey, "ES256")
                .AddValidationKey(new SecurityKeyInfo { Key = rsaKey, SigningAlgorithm = "RS256" });
        };
        pipeline.Initialize("/ROOT");

        var result = await pipeline.BackChannelClient.GetAsync("https://server/root/.well-known/openid-configuration/jwks");

        var json = await result.Content.ReadAsStringAsync();
        var jwks = new JsonWebKeySet(json);

        jwks.Keys.ShouldContain(x => x.KeyId == ecdsaKey.KeyId && x.Alg == "ES256");
        jwks.Keys.ShouldContain(x => x.KeyId == rsaKey.KeyId && x.Alg == "RS256");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Unicode_values_in_url_should_be_processed_correctly()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();

        var result = await pipeline.BackChannelClient.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = "https://грант.рф",
            Policy =
            {
                ValidateIssuerName = false,
                ValidateEndpoints = false,
                RequireHttps = false,
                RequireKeySet = false
            }
        });

        result.Issuer.ShouldBe("https://грант.рф");
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task prompt_values_supported_should_contain_defaults()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/openid-configuration");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        var prompts = data["prompt_values_supported"].EnumerateArray()
            .Select(x => x.GetString()).ToList();
        prompts.ShouldBe(["none", "login", "consent", "select_account"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task createaccount_options_should_include_create_in_prompt_values_supported()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += services =>
        {
            services.PostConfigure<IdentityServerOptions>(opts =>
            {
                opts.UserInteraction.CreateAccountUrl = "/account/create";
            });
        };
        pipeline.Initialize();


        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/openid-configuration");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        var prompts = data["prompt_values_supported"].EnumerateArray()
            .Select(x => x.GetString()).ToList();
        prompts.ShouldContain("create");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task prompt_values_supported_should_be_absent_if_no_authorize_endpoint_enabled()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();
        pipeline.Options.Endpoints.EnableAuthorizeEndpoint = false;

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/openid-configuration");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data.ContainsKey("prompt_values_supported").ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Can_enable_preview_feature_of_document_discovery_cache()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize("/root");

        pipeline.Options.Preview.EnableDiscoveryDocumentCache = true;

        pipeline.Options.Preview.DiscoveryDocumentCacheDuration = TimeSpan.FromSeconds(1);

        // cache
        _ = await pipeline.BackChannelClient.GetAsync("https://server/root/.well-known/openid-configuration");

        // add new entry
        pipeline.Options.Discovery.CustomEntries = new() {
            { "after_cache_key", "test_value" }
        };

        // get cached document
        var result = await pipeline.BackChannelClient.GetAsync("https://server/root/.well-known/openid-configuration");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        // we got a result back
        data.ContainsKey("after_cache_key").ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public void Cannot_set_entries_for_document_discovery_cache_if_enabled()
    {
        var result = new DiscoveryDocumentResult("{}", null);

        Should.Throw<InvalidOperationException>(() =>
            result.Entries = new Dictionary<string, object>());
    }

    [Fact]
    [Trait("Category", Category)]
    public void Cannot_get_entries_for_document_discovery_cache_if_enabled()
    {
        var result = new DiscoveryDocumentResult("{}", null);

        Should.Throw<InvalidOperationException>(() =>
            result.Entries.Add("Joe", "Good Stuff"));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task par_is_included_in_mtls_aliases()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();

        pipeline.Options.MutualTls.Enabled = true;


        var result = await pipeline.BackChannelClient.GetDiscoveryDocumentAsync("https://server/.well-known/openid-configuration");
        result.MtlsEndpointAliases.PushedAuthorizationRequestEndpoint.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dpop_signing_algorithms_supported_respects_configuration()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();

        var supportedAlgorithms = new List<string>
        {
            SecurityAlgorithms.RsaSha256,
            SecurityAlgorithms.EcdsaSha256
        };
        pipeline.Options.DPoP.SupportedDPoPSigningAlgorithms = supportedAlgorithms;

        var result = await pipeline.BackChannelClient.GetDiscoveryDocumentAsync("https://server/.well-known/openid-configuration");

        var supportedAlgorithmsFromResponse = result.TryGetStringArray(OidcConstants.Discovery.DPoPSigningAlgorithmsSupported);
        supportedAlgorithmsFromResponse.ShouldBe(supportedAlgorithms);
    }
}
