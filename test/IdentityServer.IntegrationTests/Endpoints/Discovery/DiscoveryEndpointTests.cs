// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Generic;
using FluentAssertions;
using IdentityModel.Client;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Xunit;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace IntegrationTests.Endpoints.Discovery;

public class DiscoveryEndpointTests
{
    private const string Category = "Discovery endpoint";

    [Fact]
    [Trait("Category", Category)]
    public async Task Issuer_uri_should_be_lowercase()
    {
        IdentityServerPipeline pipeline = new IdentityServerPipeline();
        pipeline.Initialize("/ROOT");

        var result = await pipeline.BackChannelClient.GetAsync("HTTPS://SERVER/ROOT/.WELL-KNOWN/OPENID-CONFIGURATION");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().Should().Be("https://server/root");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_lower_case_issuer_option_disabled_issuer_uri_should_be_preserved()
    {
        IdentityServerPipeline pipeline = new IdentityServerPipeline();
        pipeline.Initialize("/ROOT");

        pipeline.Options.LowerCaseIssuerUri = false;

        var result = await pipeline.BackChannelClient.GetAsync("HTTPS://SERVER/ROOT/.WELL-KNOWN/OPENID-CONFIGURATION");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().Should().Be("https://server/ROOT");
    }

    private void Pipeline_OnPostConfigureServices(IServiceCollection obj)
    {
        throw new System.NotImplementedException();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Algorithms_supported_should_match_signing_key()
    {
        var key = CryptoHelper.CreateECDsaSecurityKey(JsonWebKeyECTypes.P256);
        var expectedAlgorithm = SecurityAlgorithms.EcdsaSha256;

        IdentityServerPipeline pipeline = new IdentityServerPipeline();
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

        algorithmsSupported.Count().Should().Be(2);
        algorithmsSupported.Should().Contain(SecurityAlgorithms.RsaSha256);
        algorithmsSupported.Should().Contain(SecurityAlgorithms.EcdsaSha256);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Jwks_entries_should_countain_crv()
    {
        var ecdsaKey = CryptoHelper.CreateECDsaSecurityKey(JsonWebKeyECTypes.P256);
        var parameters = ecdsaKey.ECDsa.ExportParameters(true);

        IdentityServerPipeline pipeline = new IdentityServerPipeline();

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
        keys.Should().NotBeNull();
        keys.Count.Should().Be(2);
            
        var key = keys[1];
        key.Should().NotBeNull();

        var crv = key.TryGetValue("crv");
        crv.GetString().Should().Be(JsonWebKeyECTypes.P256);

    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Jwks_entries_should_contain_alg()
    {
        IdentityServerPipeline pipeline = new IdentityServerPipeline();
        pipeline.Initialize("/ROOT");

        var result = await pipeline.BackChannelClient.GetAsync("https://server/root/.well-known/openid-configuration/jwks");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        var keys = data["keys"];
        keys.Should().NotBeNull();

        var key = keys[0];
        key.Should().NotBeNull();

        var alg = key.TryGetValue("alg");
        alg.GetString().Should().Be(Constants.SigningAlgorithms.RSA_SHA_256);
    }

    [Theory]
    [InlineData(JsonWebKeyECTypes.P256, SecurityAlgorithms.EcdsaSha256)]
    [InlineData(JsonWebKeyECTypes.P384, SecurityAlgorithms.EcdsaSha384)]
    [InlineData(JsonWebKeyECTypes.P521, SecurityAlgorithms.EcdsaSha512)]
    [Trait("Category", Category)]
    public async Task Jwks_with_ecdsa_should_have_parsable_key(string crv, string alg)
    {
        var key = CryptoHelper.CreateECDsaSecurityKey(crv);

        IdentityServerPipeline pipeline = new IdentityServerPipeline();
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
        matchingKey.Should().NotBeNull();
        matchingKey.Should().BeOfType<ECDsaSecurityKey>();
    }

    [Fact]
    public async Task Jwks_with_two_key_using_different_algs_expect_different_alg_values()
    {
        var ecdsaKey = CryptoHelper.CreateECDsaSecurityKey();
        var rsaKey = CryptoHelper.CreateRsaSecurityKey();

        IdentityServerPipeline pipeline = new IdentityServerPipeline();
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

        jwks.Keys.Should().Contain(x => x.KeyId == ecdsaKey.KeyId && x.Alg == "ES256");
        jwks.Keys.Should().Contain(x => x.KeyId == rsaKey.KeyId && x.Alg == "RS256");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Jwks_x5c_should_not_escape_plus_character()
    {
        var cert = TestCert.Load();

        IdentityServerPipeline pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += services =>
        {
            services.AddIdentityServerBuilder()
                .AddSigningCredential(cert);
        };
        pipeline.Initialize();

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/openid-configuration/jwks");
        var json = await result.Content.ReadAsStringAsync();

        // The x5c property contains base64-encoded certificate data which commonly has '+' characters.
        // These should not be escaped as \u002B in the JSON response.
        json.Should().NotContain("\\u002B");
        json.Should().Contain("+");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Jwks_x5t_should_not_escape_base64url_encoded_characters()
    {
        var cert = TestCert.Load();

        IdentityServerPipeline pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += services =>
        {
            services.AddIdentityServerBuilder()
                .AddSigningCredential(cert);
        };
        pipeline.Initialize();

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/openid-configuration/jwks");
        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        var keys = data["keys"].EnumerateArray().ToList();
        var keyWithX5t = keys.First(k => k.TryGetProperty("x5t", out _));
        var x5t = keyWithX5t.GetProperty("x5t").GetString();

        // The x5t property is a base64url-encoded SHA-1 thumbprint (per RFC 7517).
        // Base64url encoding uses '-' and '_' instead of '+' and '/', so '+' and '/' must not appear.
        x5t.Should().NotContain("+");
        x5t.Should().NotContain("/");
        x5t.Should().Contain("_"); // The cert we are using happens to contain '_' but not '-' in its thumbprint

        // Verify the value matches the expected base64url-encoded thumbprint
        var expectedThumbprint = Base64UrlEncoder.Encode(cert.GetCertHash());
        x5t.Should().Be(expectedThumbprint);
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

        result.Issuer.Should().Be("https://грант.рф");
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task prompt_values_supported_should_contain_defaults()
    {
        IdentityServerPipeline pipeline = new IdentityServerPipeline();
        pipeline.Initialize();

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/openid-configuration");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        var prompts = data["prompt_values_supported"].EnumerateArray()
            .Select(x => x.GetString()).ToList();
        prompts.Should().BeEquivalentTo(new[] { "none", "login", "consent", "select_account" });
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task createaccount_options_should_include_create_in_prompt_values_supported()
    {
        IdentityServerPipeline pipeline = new IdentityServerPipeline();
        pipeline.OnPostConfigureServices += services =>
        {
            services.PostConfigure<IdentityServerOptions>(opts => { 
                opts.UserInteraction.CreateAccountUrl = "/account/create";
            });
        };
        pipeline.Initialize();
        

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/openid-configuration");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        var prompts = data["prompt_values_supported"].EnumerateArray()
            .Select(x => x.GetString()).ToList();
        prompts.Should().Contain("create");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task prompt_values_supported_should_be_absent_if_no_authorize_endpoint_enabled()
    {
        IdentityServerPipeline pipeline = new IdentityServerPipeline();
        pipeline.Initialize();
        pipeline.Options.Endpoints.EnableAuthorizeEndpoint = false;

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/openid-configuration");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data.ContainsKey("prompt_values_supported").Should().BeFalse();
    }
}