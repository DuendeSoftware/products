// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UnitTests.Common;
using UnitTests.Validation.Setup;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace UnitTests.Validation.Secrets;

public class AttestationSecretValidation
{
    private readonly IIssuerNameService _issuerNameService;
    private readonly StubClock _clock;
    private readonly IdentityServerOptions _options;
    private readonly TestReplayCache _replayCache;
    private readonly ILogger<AttestationSecretValidator> _logger;
    private readonly AttestationSecretValidator _subject;

    private readonly IClientStore _clients;
    private readonly string _privateKeyJwk = "{\"d\":\"AmdW9v0RgI8xzo4pe7jY1HMUCwxlPLdFXJhS_lm61Uwbx-KOW0DKvXByP-q0cvMWJU7BIT7TFOykpTn_-y9GyvHhbI_l7bFnhvUDNZ8DbayIXdRt7liAwOdEFmaRcgD0Mx3TAV-GZZc4I8IKBIH9fKyZECYe1jmNxEx2fuVdnQEvnAZeDsR4QUkVDy3QxNieRRTuPa5WiXoXgGBbTYxDiumII5CteM79HYHjD9rE5zhlVvFcPOWiQMlrSFoZgSJITrrKHhPOazR9rb7kacmtNaC_vLw6AQGPflmRk2Qw4CSWDfpePAs_4lF3Cd_4wr4QZI2wVziPWv0T-WCm3nZyNQ\",\"dp\":\"FNnpZ2fo7QQvLPMGe54i7IlotYWI1wFu9irCbVjFmwXi22mM9yxmfKF2YeLoS7bEj2Oxz4PKkTbgcP6LdBHp8HKRycbMRBiRqFxQLwZDSdzdlpWNH2oZdrTccTo_NboYYIW26iO84wV43_Y5N0Hlyi4aeN6oNiQC641JZu0KSd0\",\"dq\":\"fgVRYnzeu5u7r8ECGB0U6PxSNo3Fq0a0KuwQEy2NC7Dy5vNN8pFwPfMYbwdGOW6PVRekWcY9f8PfH7Ph5e-Mp5EibK5ESgHIltdc_OaTNyXugToAIXOov8xn2ESzcVBi-fZMhTcI49pWte8mLrdt79BgbRR7v0aSykN3B8kEb28\",\"e\":\"AQAB\",\"key_ops\":[],\"kid\":\"30A14F8199B04DA9B8EA0490B558AF0C\",\"kty\":\"RSA\",\"n\":\"xPYA343z2Ih4p3S2qiHZXX98S63lFbxshzqy8topTItjSWf5qlR_uM9P8bW7AheGPVwAxKR_cIQejtWPU6_5B3MG3QW59rI6HTxrnyaueLjDXWXmpEakM_y7dc6oi439E-qoXHsX2doEJ1zxg_CBpyxHbMIHJvuUiK8EmJHdCaxORB7y7VKrqNCLW1vP-9lx8lW4MZUbwlX7FbsPhxlqfoA5FrYtDzKyM3n1wLQlsJN7n6GVLTwhoy7V80CsPuqQ577abVhrfyPZplxLCRAQVoyhAovXHr4h4mS0vQDYyyHuNi4DzrxHjTfyeLhWLOQa_KsF7ZAWOjXD7K8lWVToPQ\",\"oth\":[],\"p\":\"93gtTvc0_G91V-hsO-0Ee1VEeHRhYY3cMQk08rQy_Iu5ONs1KYJZPpVrPzO_CDVrYIiKHb6i3O0RtBumvTZVmOs93Dy7zKv79qJJCF2Rq0LTYEvU1lUoXt4eI1u4az6u_cdqKF0Lt8mpR9Dz7B3nhogkq2Yih2Gu8URg_rjwpn8\",\"q\":\"y8AbvJB7VSr5BKI4QNFAqkZC8zx4_KYYTL9uFMnSCx2EJt34pd4AlvzM6hoqtcdN0va7SieEm3JMOcsftYlSgb6yhrbxFzw8Jy5mLJ9uElW8yOmu5Htea4kWxZDbg-lRAvHc0cYZkiSOzvi-g2-093t59-jDmmKZNRilIjyeK0M\",\"qi\":\"FRKbtYPrSpODbSuuh8xSeonSqNhBDrkUbnwGSvA8FUBNqOiEnGiMjin1t9UXWckgY8-GYztPKWp-UcPIcBAKRlHJX9ag5jusmWmPRSCu2aHOqzmEBY8cyAmZVzwiLF3gJmiORpv4XDkFJaPj904HI8Z7dywCHseUTAwZvteEhEI\",\"x5c\":[]}";
    private readonly string _publicKeyJwk = "{\"e\":\"AQAB\",\"key_ops\":[],\"kty\":\"RSA\",\"n\":\"xPYA343z2Ih4p3S2qiHZXX98S63lFbxshzqy8topTItjSWf5qlR_uM9P8bW7AheGPVwAxKR_cIQejtWPU6_5B3MG3QW59rI6HTxrnyaueLjDXWXmpEakM_y7dc6oi439E-qoXHsX2doEJ1zxg_CBpyxHbMIHJvuUiK8EmJHdCaxORB7y7VKrqNCLW1vP-9lx8lW4MZUbwlX7FbsPhxlqfoA5FrYtDzKyM3n1wLQlsJN7n6GVLTwhoy7V80CsPuqQ577abVhrfyPZplxLCRAQVoyhAovXHr4h4mS0vQDYyyHuNi4DzrxHjTfyeLhWLOQa_KsF7ZAWOjXD7K8lWVToPQ\",\"oth\":[],\"x5c\":[]}";
    private readonly SigningCredentials _defaultSigningCredential;

    private const string AuthServerIdentity = "https://identityserver.io";
    private const string ValidClientId = "attestation_client_valid";

    public AttestationSecretValidation()
    {
        _issuerNameService = new TestIssuerNameService(AuthServerIdentity);
        _clock = new StubClock();
        _options = new IdentityServerOptions();
        _replayCache = new TestReplayCache(_clock);
        _logger = new NullLogger<AttestationSecretValidator>();
        _subject = new AttestationSecretValidator(_issuerNameService, _replayCache, Options.Create(_options), _logger);

        _defaultSigningCredential = new SigningCredentials(new JsonWebKey(_privateKeyJwk), SecurityAlgorithms.RsaSha256);

        _clients = new InMemoryClientStore(ClientValidationTestClients.Get());
    }

    [Fact]
    public async Task Invalid_Client_Secrets()
    {
        var client = await _clients.FindClientByIdAsync("attestation_client_invalid");
        var (attestationJwt, popJwt) = CreateValidJwtPair();
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Attestation_Jwt_Format()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (_, popJwt) = CreateValidJwtPair();
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = "not.a.valid.jwt",
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Token_Type_In_Attestation_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (_, popJwt) = CreateValidJwtPair();
        var attestationJwt = CreateAttestationJwt(typValue: "invalid-type");
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Attestation_Jwt_Not_Signed_By_Key_In_ClientSecrets()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (_, popJwt) = CreateValidJwtPair();
        var signingKey = CryptoHelper.CreateRsaSecurityKey();
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);
        var attestationJwt = CreateAttestationJwt(signingCredentials: signingCredentials);
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Unsupported_Algorithm_In_Attestation_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (_, popJwt) = CreateValidJwtPair();
        var signingKey = new SymmetricSecurityKey("a-secret-key-of-at-least-128-bits"u8.ToArray());
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var attestationJwt = CreateAttestationJwt(signingCredentials: signingCredentials);
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    //[Theory]
    // [InlineData(null)]
    // [InlineData("")]
    // [InlineData("invalid-issuer")]
    // public async Task MissingOrInvalidIssuerInAttestationJWT_ShouldReturnNull(string issuer)
    // {
    //     var context = new DefaultHttpContext();
    //     var (_, popJwt) = CreateValidJwtPair();
    //     var attestationJwt = CreateAttestationJwt(issValue: issuer);
    //     context.Request.Headers["OAuth-Client-Attestation"] = attestationJwt;
    //     context.Request.Headers["OAuth-Client-Attestation-PoP"] = popJwt;
    //     context.Request.Query = new QueryCollection(
    //         new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
    //         {
    //             { "client_id", ValidClientId }
    //         });
    //
    //     var secret = await _parser.ParseAsync(context);
    //
    //     secret.ShouldBeNull();
    // }

    [Theory]
    [InlineData("")]
    [InlineData("different_client")]
    public async Task Missing_Or_Invalid_Subject_In_Attestation_Jwt(string subject)
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (_, popJwt) = CreateValidJwtPair();
        var attestationJwt = CreateAttestationJwt(subValue: subject);
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Expired_Attestation_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (_, popJwt) = CreateValidJwtPair();
        var expTime = _clock.UtcNow.AddHours(-1).ToUnixTimeSeconds(); // 1 hour in the past
        var attestationJwt = CreateAttestationJwt(expValue: expTime);
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Cnf_Claim_In_Attestation_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (_, popJwt) = CreateValidJwtPair();
        var cnfValue = new Dictionary<string, object> { { "invalid", "value" } };
        var attestationJwt = CreateAttestationJwt(cnfValue: JsonSerializer.Serialize(cnfValue));
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Nbf_Claim_InAttestation_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (_, popJwt) = CreateValidJwtPair();
        var notBeforeTime = _clock.UtcNow.AddHours(1).ToUnixTimeSeconds(); // 1 hour in the future
        var attestationJwt = CreateAttestationJwt(nbfValue: notBeforeTime);
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Pop_Jwt_Format()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (attestationJwt, _) = CreateValidJwtPair();
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = "not.a.valid.jwt"
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Token_Type_In_Pop_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (attestationJwt, _) = CreateValidJwtPair();
        var popJwt = CreatePopJwt(typValue: "invalid-type");
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Unsupported_Algorithm_In_PoP_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (attestationJwt, _) = CreateValidJwtPair();
        var signingKey = new SymmetricSecurityKey("a-secret-key-of-at-least-128-bits"u8.ToArray());
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var popJwt = CreatePopJwt(signingCredentials: signingCredentials);
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-issuer")]
    public async Task Missing_Or_Invalid_Issuer_In_PoP_Jwt(string issuer)
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (attestationJwt, _) = CreateValidJwtPair();
        var popJwt = CreatePopJwt(issuer);
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Expired_PoP_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (attestationJwt, _) = CreateValidJwtPair();
        var expTime = _clock.UtcNow.AddHours(-1).ToUnixTimeSeconds(); // 1 hour in the past
        var popJwt = CreatePopJwt(expValue: expTime);
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-audience")]
    public async Task Missing_Or_Invalid_Audience_In_PoP_Jwt(string audience)
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (attestationJwt, _) = CreateValidJwtPair();
        var popJwt = CreatePopJwt(audValue: audience);
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Missing_Jti_In_PoP_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (attestationJwt, _) = CreateValidJwtPair();
        var popJwt = CreatePopJwt(jtiValue: "");
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    //TODO: nonce tests once we have a better understanding

    //TODO?: test IAT claim?

    [Fact]
    public async Task Invalid_Nbf_Claim_In_PoP_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (attestationJwt, _) = CreateValidJwtPair();
        var notBeforeTime = _clock.UtcNow.AddHours(1).ToUnixTimeSeconds(); // 1 hour in the future
        var popJwt = CreatePopJwt(nbfValue: notBeforeTime);
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task PoP_Jwt_Not_Signed_With_Cnf_Claim_In_Attestation_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (attestationJwt, _) = CreateValidJwtPair();
        var rsaKey = CryptoHelper.CreateRsaSecurityKey();
        var signingCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        var popJwt = CreatePopJwt(signingCredentials: signingCredentials);
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Replayed_Valid_Tokens()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (attestationJwt, popJwt) = CreateValidJwtPair();
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeTrue();

        var replayedResult = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        replayedResult.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Valid_Tokens()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (attestationJwt, popJwt) = CreateValidJwtPair();
        var context = new AttestationSecretValidationContext
        {
            ClientId = ValidClientId,
            ClientAttestationJwt = attestationJwt,
            ClientAttestationPopJwt = popJwt
        };
        var parsedSecret = new ParsedSecret
        {
            Id = ValidClientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = context
        };

        var result = await _subject.ValidateAsync(client.ClientSecrets, parsedSecret);

        result.Success.ShouldBeTrue();
    }

    private (string attestationJwt, string popJwt) CreateValidJwtPair()
    {
        var jwk = new JsonWebKey(_publicKeyJwk)
        {
            KeyId = "test-key-id"
        };

        // Current time values
        var now = _clock.UtcNow.ToUnixTimeSeconds();
        var exp = now + 3600; // Valid for 1 hour

        var attestationJwt = CreateAttestationJwt(
            typValue: "oauth-client-attestation+jwt",
            issValue: "https://client-issuer.example.com",
            subValue: ValidClientId,
            expValue: exp,
            iatValue: now,
            nbfValue: now,
            audValue: AuthServerIdentity,
            cnfValue: JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "jwk", JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(jwk)) }
            }),
            signingCredentials: _defaultSigningCredential
        );

        var popJwt = CreatePopJwt(
            typValue: "oauth-client-attestation-pop+jwt",
            issValue: ValidClientId,
            expValue: exp,
            iatValue: now,
            nbfValue: now,
            audValue: AuthServerIdentity,
            jtiValue: Guid.NewGuid().ToString(),
            nonceValue: "test-nonce",
            signingCredentials: _defaultSigningCredential
        );

        return (attestationJwt, popJwt);
    }

    private string CreateAttestationJwt(
        string typValue = "oauth-client-attestation+jwt",
        bool includeIss = true,
        string issValue = "https://client-issuer.example.com",
        string subValue = null,
        long? expValue = null,
        long? iatValue = null,
        long? nbfValue = null,
        string audValue = null,
        string cnfValue = null,
        SigningCredentials signingCredentials = null)
    {
        subValue ??= ValidClientId;
        expValue ??= _clock.UtcNow.AddHours(1).ToUnixTimeSeconds();
        iatValue ??= _clock.UtcNow.ToUnixTimeSeconds();
        nbfValue ??= _clock.UtcNow.ToUnixTimeSeconds();
        audValue ??= AuthServerIdentity;
        signingCredentials ??= _defaultSigningCredential;

        var header = new Dictionary<string, object>
        {
            { "typ", typValue }
        };

        var payload = new Dictionary<string, object>();

        if (includeIss)
        {
            payload["iss"] = issValue;
        }

        payload["sub"] = subValue;
        payload["exp"] = expValue;
        payload["iat"] = iatValue;
        payload["nbf"] = nbfValue;
        payload["aud"] = audValue;

        if (cnfValue != null)
        {
            payload["cnf"] = cnfValue;
        }
        else
        {
            var jwk = signingCredentials.Key;
            jwk.KeyId = "test-key-id";
            payload["cnf"] = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "jwk", JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(jwk)) }
            });
        }

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(JsonSerializer.Serialize(payload), signingCredentials, header);

        return token;
    }

    private string CreatePopJwt(
        string typValue = "oauth-client-attestation-pop+jwt",
        string issValue = null,
        long? expValue = null,
        long? iatValue = null,
        long? nbfValue = null,
        string audValue = null,
        string jtiValue = null,
        string nonceValue = null,
        SigningCredentials signingCredentials = null)
    {
        issValue ??= ValidClientId;
        expValue ??= _clock.UtcNow.AddHours(1).ToUnixTimeSeconds();
        iatValue ??= _clock.UtcNow.ToUnixTimeSeconds();
        nbfValue ??= _clock.UtcNow.ToUnixTimeSeconds();
        audValue ??= AuthServerIdentity;
        jtiValue ??= Guid.NewGuid().ToString();
        nonceValue ??= "test-nonce";
        signingCredentials ??= _defaultSigningCredential;

        var header = new Dictionary<string, object>
        {
            { "typ", typValue }
        };

        var payload = new Dictionary<string, object>
        {
            { "iss", issValue },
            { "exp", expValue },
            { "iat", iatValue },
            { "nbf", nbfValue },
            { "aud", audValue },
            { "jti", jtiValue },
            { "nonce", nonceValue }
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(JsonSerializer.Serialize(payload), signingCredentials, header);

        return token;
    }
}
