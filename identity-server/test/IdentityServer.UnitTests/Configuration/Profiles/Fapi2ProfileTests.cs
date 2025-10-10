// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.Profiles;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace IdentityServer.UnitTests.Configuration.Profiles;

public class Fapi2ProfileTests
{
    private const string Category = "FAPI 2.0 Profile";

    /// <summary>
    /// Creates a client that satisfies all FAPI 2.0 requirements.
    /// Tests can modify specific properties to test individual validations.
    /// </summary>
    private static Client CreateValidFapi2Client() => new Client
    {
        RequirePkce = true,
        RequireDPoP = true,
        ClientSecrets =
            [
                new Secret { Type = IdentityServerConstants.SecretTypes.JsonWebKey }
            ]
    };

    [Fact]
    [Trait("Category", Category)]
    public void should_override_par_required_when_false()
    {
        var options = new IdentityServerOptions
        {
            PushedAuthorization = new() { Required = false }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "PushedAuthorization.Required");
        options.PushedAuthorization.Required.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_jwt_clock_skew_when_too_large()
    {
        var options = new IdentityServerOptions
        {
            JwtValidationClockSkew = TimeSpan.FromMinutes(5)
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "JwtValidationClockSkew");
        options.JwtValidationClockSkew.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_valid_jwt_clock_skew()
    {
        var options = new IdentityServerOptions
        {
            JwtValidationClockSkew = TimeSpan.FromSeconds(13)
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "JwtValidationClockSkew");
        options.JwtValidationClockSkew.ShouldBe(TimeSpan.FromSeconds(13)); // Should not be overridden
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_signing_algorithms_when_invalid()
    {
        var options = new IdentityServerOptions
        {
            KeyManagement = new()
            {
                SigningAlgorithms =
                [
                    new SigningAlgorithmOptions(SecurityAlgorithms.RsaSha256) // RS256 not allowed in FAPI 2.0
                ]
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "KeyManagement.SigningAlgorithms");
        options.KeyManagement.SigningAlgorithms.Count.ShouldBe(1);
        options.KeyManagement.SigningAlgorithms.First().Name.ShouldBe(SecurityAlgorithms.RsaSsaPssSha256);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_ps256_signing_algorithm()
    {
        var options = new IdentityServerOptions
        {
            KeyManagement = new()
            {
                SigningAlgorithms =
                [
                    new SigningAlgorithmOptions(SecurityAlgorithms.RsaSsaPssSha256) // PS256 allowed
                ]
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "KeyManagement.SigningAlgorithms");
        options.KeyManagement.SigningAlgorithms.Count.ShouldBe(1);
        options.KeyManagement.SigningAlgorithms.First().Name.ShouldBe(SecurityAlgorithms.RsaSsaPssSha256);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_es256_signing_algorithm()
    {
        var options = new IdentityServerOptions
        {
            KeyManagement = new()
            {
                SigningAlgorithms =
                [
                    new SigningAlgorithmOptions(SecurityAlgorithms.EcdsaSha256) // ES256 allowed
                ]
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "KeyManagement.SigningAlgorithms");
        options.KeyManagement.SigningAlgorithms.Count.ShouldBe(1);
        options.KeyManagement.SigningAlgorithms.First().Name.ShouldBe(SecurityAlgorithms.EcdsaSha256);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_both_ps256_and_es256()
    {
        var options = new IdentityServerOptions
        {
            KeyManagement = new()
            {
                SigningAlgorithms =
                [
                    new SigningAlgorithmOptions(SecurityAlgorithms.RsaSsaPssSha256),
                    new SigningAlgorithmOptions(SecurityAlgorithms.EcdsaSha256)
                ]
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "KeyManagement.SigningAlgorithms");
        options.KeyManagement.SigningAlgorithms.Count.ShouldBe(2);
        options.KeyManagement.SigningAlgorithms.Select(a => a.Name).ShouldBe([
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.EcdsaSha256
        ]);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_reject_mix_of_valid_and_invalid_algorithms()
    {
        var options = new IdentityServerOptions
        {
            KeyManagement = new()
            {
                SigningAlgorithms =
                [
                    new SigningAlgorithmOptions(SecurityAlgorithms.RsaSsaPssSha256), // PS256 - valid
                    new SigningAlgorithmOptions(SecurityAlgorithms.RsaSha256)         // RS256 - invalid
                ]
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "KeyManagement.SigningAlgorithms");
        // Should be overridden to default PS256 only
        options.KeyManagement.SigningAlgorithms.Count.ShouldBe(1);
        options.KeyManagement.SigningAlgorithms.First().Name.ShouldBe(SecurityAlgorithms.RsaSsaPssSha256);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_empty_signing_algorithms()
    {
        var options = new IdentityServerOptions
        {
            KeyManagement = new()
            {
                SigningAlgorithms = []
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "KeyManagement.SigningAlgorithms");
        options.KeyManagement.SigningAlgorithms.Count.ShouldBe(1);
        options.KeyManagement.SigningAlgorithms.First().Name.ShouldBe(SecurityAlgorithms.RsaSsaPssSha256);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_dpop_algorithms_with_defaults()
    {
        var options = new IdentityServerOptions(); // Uses default DPoP algorithms
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "DPoP.SupportedDPoPSigningAlgorithms");
        options.DPoP.SupportedDPoPSigningAlgorithms.Count.ShouldBe(2);
        options.DPoP.SupportedDPoPSigningAlgorithms.ShouldBe([
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.EcdsaSha256
        ]);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_valid_dpop_algorithms()
    {
        var options = new IdentityServerOptions
        {
            DPoP = new()
            {
                SupportedDPoPSigningAlgorithms = [
                    SecurityAlgorithms.RsaSsaPssSha256,
                    SecurityAlgorithms.EcdsaSha256
                ]
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "DPoP.SupportedDPoPSigningAlgorithms");
        options.DPoP.SupportedDPoPSigningAlgorithms.ShouldBe([
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.EcdsaSha256
        ]);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_invalid_dpop_algorithms()
    {
        var options = new IdentityServerOptions
        {
            DPoP = new()
            {
                SupportedDPoPSigningAlgorithms = [SecurityAlgorithms.RsaSha256] // Invalid for FAPI 2.0
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "DPoP.SupportedDPoPSigningAlgorithms");
        options.DPoP.SupportedDPoPSigningAlgorithms.ShouldBe([
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.EcdsaSha256
        ]);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_client_assertion_algorithms_with_defaults()
    {
        var options = new IdentityServerOptions(); // Uses default client assertion algorithms
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "SupportedClientAssertionSigningAlgorithms");
        options.SupportedClientAssertionSigningAlgorithms.Count.ShouldBe(2);
        options.SupportedClientAssertionSigningAlgorithms.ShouldBe([
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.EcdsaSha256
        ]);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_valid_client_assertion_algorithms()
    {
        var options = new IdentityServerOptions
        {
            SupportedClientAssertionSigningAlgorithms = [
                SecurityAlgorithms.EcdsaSha256,
                SecurityAlgorithms.RsaSsaPssSha256
            ]
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "SupportedClientAssertionSigningAlgorithms");
        options.SupportedClientAssertionSigningAlgorithms.ShouldBe([
            SecurityAlgorithms.EcdsaSha256,
            SecurityAlgorithms.RsaSsaPssSha256
        ]);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_request_object_algorithms_with_defaults()
    {
        var options = new IdentityServerOptions(); // Uses default request object algorithms
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "SupportedRequestObjectSigningAlgorithms");
        options.SupportedRequestObjectSigningAlgorithms.Count.ShouldBe(2);
        options.SupportedRequestObjectSigningAlgorithms.ShouldBe([
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.EcdsaSha256
        ]);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_valid_request_object_algorithms()
    {
        var options = new IdentityServerOptions
        {
            SupportedRequestObjectSigningAlgorithms = [SecurityAlgorithms.RsaSsaPssSha256]
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "SupportedRequestObjectSigningAlgorithms");
        options.SupportedRequestObjectSigningAlgorithms.ShouldBe([SecurityAlgorithms.RsaSsaPssSha256]);
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_pkce_disabled_should_be_overridden()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.RequirePkce = false;

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "RequirePkce");
        client.RequirePkce.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_pkce_enabled_should_pass_validation()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.IsValid.ShouldBeTrue();
        result.Failed.Count.ShouldBe(0);
        result.Passed.ShouldContain(check => check.Path == "RequirePkce");
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_issuer_identification_parameter_when_false()
    {
        var options = new IdentityServerOptions
        {
            EmitIssuerIdentificationResponseParameter = false
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "EmitIssuerIdentificationResponseParameter");
        options.EmitIssuerIdentificationResponseParameter.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_issuer_identification_parameter_when_true()
    {
        var options = new IdentityServerOptions
        {
            EmitIssuerIdentificationResponseParameter = true
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "EmitIssuerIdentificationResponseParameter");
        options.EmitIssuerIdentificationResponseParameter.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_empty_allowed_identity_token_signing_algorithms_should_pass()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.AllowedIdentityTokenSigningAlgorithms = [];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "AllowedIdentityTokenSigningAlgorithms");
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_valid_identity_token_signing_algorithms_should_pass()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.AllowedIdentityTokenSigningAlgorithms =
        [
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.EcdsaSha256
        ];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "AllowedIdentityTokenSigningAlgorithms");
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_invalid_identity_token_signing_algorithms_should_warn()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.AllowedIdentityTokenSigningAlgorithms =
        [
            SecurityAlgorithms.RsaSha256
        ];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.IsValid.ShouldBeFalse();
        result.Failed.ShouldContain(check =>
            check.Path == "AllowedIdentityTokenSigningAlgorithms" &&
            check.Description.Contains("PS256 or ES256"));
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_dpop_should_pass_sender_constraining_validation()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.RequireDPoP = true;

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "RequireDPoP");
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_mtls_always_emit_should_pass_sender_constraining_validation()
    {
        var options = new IdentityServerOptions
        {
            MutualTls = new()
            {
                Enabled = true,
                AlwaysEmitConfirmationClaim = true
            }
        };
        var client = CreateValidFapi2Client();
        client.RequireDPoP = false;

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "RequireDPoP");
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_mtls_authentication_with_always_emit_should_pass_sender_constraining_validation()
    {
        var options = new IdentityServerOptions
        {
            MutualTls = new()
            {
                Enabled = true,
                AlwaysEmitConfirmationClaim = true // Required when using mTLS authentication
            }
        };
        var client = CreateValidFapi2Client();
        client.RequireDPoP = false;
        client.ClientSecrets =
        [
            new Secret { Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint }
        ];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "RequireDPoP");
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_without_sender_constraining_should_warn()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.RequireDPoP = false;
        client.ClientSecrets =  // Change to shared secret so mTLS auth check fails
        [
            new Secret("shared-secret".Sha256())
        ];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.IsValid.ShouldBeFalse();
        result.Failed.ShouldContain(check =>
            check.Path == "RequireDPoP" &&
            check.Description.Contains("sender-constraining"));
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_mtls_certificate_secrets_should_pass_authentication_validation()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.ClientSecrets =
        [
            new Secret { Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint }
        ];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        // Note: This client will still fail sender-constraining validation unless mTLS options are set, but should pass secret type validation
        result.Passed.ShouldContain(check => check.Path == "ClientSecrets");
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_jwk_secrets_should_pass_authentication_validation()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.ClientSecrets =
        [
            new Secret { Type = IdentityServerConstants.SecretTypes.JsonWebKey }
        ];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "ClientSecrets");
        result.Failed.ShouldNotContain(check => check.Path == "ClientSecrets");
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_shared_secret_should_warn_about_authentication()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.ClientSecrets =
        [
            new Secret("shared-secret".Sha256())
        ];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.IsValid.ShouldBeFalse();
        result.Failed.ShouldContain(check =>
            check.Path == "ClientSecrets" &&
            check.Description.Contains("mTLS") &&
            check.Description.Contains("client assertions"));
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_without_secrets_should_warn_about_authentication()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.ClientSecrets = [];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.IsValid.ShouldBeFalse();
        result.Failed.ShouldContain(check => check.Path == "ClientSecrets");
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_discovery_endpoint_when_disabled()
    {
        var options = new IdentityServerOptions
        {
            Endpoints = new()
            {
                EnableDiscoveryEndpoint = false
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "Endpoints.EnableDiscoveryEndpoint");
        options.Endpoints.EnableDiscoveryEndpoint.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_discovery_endpoint_when_enabled()
    {
        var options = new IdentityServerOptions
        {
            Endpoints = new()
            {
                EnableDiscoveryEndpoint = true
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "Endpoints.EnableDiscoveryEndpoint");
        options.Endpoints.EnableDiscoveryEndpoint.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_strict_client_assertion_audience_validation_when_disabled()
    {
        var options = new IdentityServerOptions
        {
            Preview = new()
            {
                StrictClientAssertionAudienceValidation = false
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "Preview.StrictClientAssertionAudienceValidation");
        options.Preview.StrictClientAssertionAudienceValidation.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_strict_client_assertion_audience_validation_when_enabled()
    {
        var options = new IdentityServerOptions
        {
            Preview = new()
            {
                StrictClientAssertionAudienceValidation = true
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "Preview.StrictClientAssertionAudienceValidation");
        options.Preview.StrictClientAssertionAudienceValidation.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_jwt_clock_skew_when_below_minimum()
    {
        var options = new IdentityServerOptions
        {
            JwtValidationClockSkew = TimeSpan.FromSeconds(5) // Less than 10 seconds
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "JwtValidationClockSkew");
        options.JwtValidationClockSkew.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_jwt_clock_skew_when_above_maximum()
    {
        var options = new IdentityServerOptions
        {
            JwtValidationClockSkew = TimeSpan.FromSeconds(70) // Greater than 60 seconds
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "JwtValidationClockSkew");
        options.JwtValidationClockSkew.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_jwt_clock_skew_within_valid_range()
    {
        var options = new IdentityServerOptions
        {
            JwtValidationClockSkew = TimeSpan.FromSeconds(30) // Between 10 and 60
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "JwtValidationClockSkew");
        options.JwtValidationClockSkew.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_pushed_authorization_endpoint_when_disabled()
    {
        var options = new IdentityServerOptions
        {
            Endpoints = new()
            {
                EnablePushedAuthorizationEndpoint = false
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "Endpoints.EnablePushedAuthorizationEndpoint");
        options.Endpoints.EnablePushedAuthorizationEndpoint.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_pushed_authorization_endpoint_when_enabled()
    {
        var options = new IdentityServerOptions
        {
            Endpoints = new()
            {
                EnablePushedAuthorizationEndpoint = true
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "Endpoints.EnablePushedAuthorizationEndpoint");
        options.Endpoints.EnablePushedAuthorizationEndpoint.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_par_lifetime_when_too_long()
    {
        var options = new IdentityServerOptions
        {
            PushedAuthorization = new()
            {
                Lifetime = 900 // Greater than 600 seconds
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "PushedAuthorization.Lifetime");
        options.PushedAuthorization.Lifetime.ShouldBe(600);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_par_lifetime_when_valid()
    {
        var options = new IdentityServerOptions
        {
            PushedAuthorization = new()
            {
                Lifetime = 300 // Less than or equal to 600
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "PushedAuthorization.Lifetime");
        options.PushedAuthorization.Lifetime.ShouldBe(300);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_override_nonce_length_restriction_when_too_small()
    {
        var options = new IdentityServerOptions
        {
            InputLengthRestrictions = new()
            {
                Nonce = 32 // Less than 64
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "InputLengthRestrictions.Nonce");
        options.InputLengthRestrictions.Nonce.ShouldBe(64);
    }

    [Fact]
    [Trait("Category", Category)]
    public void should_accept_nonce_length_restriction_when_valid()
    {
        var options = new IdentityServerOptions
        {
            InputLengthRestrictions = new()
            {
                Nonce = 128 // Greater than or equal to 64
            }
        };
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ApplyProfile(options);

        result.Passed.ShouldContain(check => check.Path == "InputLengthRestrictions.Nonce");
        options.InputLengthRestrictions.Nonce.ShouldBe(128);
    }

    // ========== New Client Validation Tests ==========

    [Fact]
    [Trait("Category", Category)]
    public void client_with_allowed_grant_types_should_pass()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.AllowedGrantTypes = [GrantType.AuthorizationCode, GrantType.ClientCredentials];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "AllowedGrantTypes");
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_password_grant_should_warn()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.AllowedGrantTypes = [GrantType.ResourceOwnerPassword];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Failed.ShouldContain(check =>
            check.Path == "AllowedGrantTypes" &&
            check.Description.Contains("forbids"));
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_implicit_grant_should_warn()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.AllowedGrantTypes = [GrantType.Implicit];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Failed.ShouldContain(check =>
            check.Path == "AllowedGrantTypes" &&
            check.Description.Contains("forbids"));
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_hybrid_grant_should_warn()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.AllowedGrantTypes = [GrantType.Hybrid];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Failed.ShouldContain(check =>
            check.Path == "AllowedGrantTypes" &&
            check.Description.Contains("forbids"));
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_long_auth_code_lifetime_should_be_overridden()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.AuthorizationCodeLifetime = 300; // Greater than 60 seconds

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "AuthorizationCodeLifetime");
        client.AuthorizationCodeLifetime.ShouldBe(60);
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_valid_auth_code_lifetime_should_pass()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.AuthorizationCodeLifetime = 30; // Less than or equal to 60

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "AuthorizationCodeLifetime");
        client.AuthorizationCodeLifetime.ShouldBe(30);
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_refresh_token_rotation_should_be_overridden()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.RefreshTokenUsage = TokenUsage.OneTimeOnly;

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "RefreshTokenUsage");
        client.RefreshTokenUsage.ShouldBe(TokenUsage.ReUse);
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_reuse_refresh_tokens_should_pass()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.RefreshTokenUsage = TokenUsage.ReUse;

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "RefreshTokenUsage");
        client.RefreshTokenUsage.ShouldBe(TokenUsage.ReUse);
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_https_redirect_uris_should_pass()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.RedirectUris = ["https://example.com/callback", "https://app.example.com/oidc"];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "RedirectUris");
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_localhost_redirect_uris_should_pass()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.RedirectUris = ["https://localhost:5001/callback", "https://127.0.0.1:8080/oidc"];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Passed.ShouldContain(check => check.Path == "RedirectUris");
    }

    [Fact]
    [Trait("Category", Category)]
    public void client_with_http_redirect_uris_should_warn()
    {
        var options = new IdentityServerOptions();
        var client = CreateValidFapi2Client();
        client.RedirectUris = ["http://example.com/callback"];

        var context = new Duende.IdentityServer.Validation.ClientConfigurationValidationContext(client);
        var logger = new NullLogger<Fapi2ConfigurationProfile>();
        var profile = new Fapi2ConfigurationProfile(logger);

        var result = profile.ValidateClient(options, context);

        result.Failed.ShouldContain(check =>
            check.Path == "RedirectUris" &&
            check.Description.Contains("https"));
    }

}
