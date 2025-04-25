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
    private readonly RsaSecurityKey _defaultPrivateKey;
    private readonly RsaSecurityKey _defaultPublicKey;

    private const string AuthServerIdentity = "https://identityserver.io";
    private const string ValidClientId = "no_secret_client";

    public AttestationSecretValidation()
    {
        _issuerNameService = new TestIssuerNameService(AuthServerIdentity);
        _clock = new StubClock();
        _options = new IdentityServerOptions();
        _replayCache = new TestReplayCache(_clock);
        _logger = new NullLogger<AttestationSecretValidator>();
        _subject = new AttestationSecretValidator(_issuerNameService, _replayCache, Options.Create(_options), _logger);

        _defaultPrivateKey = CryptoHelper.CreateRsaSecurityKey();
        _defaultPublicKey = new RsaSecurityKey(_defaultPrivateKey.Rsa.ExportParameters(false));

        _clients = new InMemoryClientStore(ClientValidationTestClients.Get());
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
    public async Task Unsupported_Algorithm_In_Attestation_Jwt()
    {
        var client = await _clients.FindClientByIdAsync(ValidClientId);
        var (_, popJwt) = CreateValidJwtPair();
        var signingKey = new SymmetricSecurityKey("a-secret-key-of-at-least-128-bits"u8.ToArray());
        var attestationJwt = CreateAttestationJwt(signingKey: signingKey, algValue: SecurityAlgorithms.HmacSha256);
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
        var popJwt = CreatePopJwt(signingKey: signingKey, algValue: SecurityAlgorithms.HmacSha256);
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
        var popJwt = CreatePopJwt(signingKey: rsaKey);
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
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(_defaultPublicKey);
        jwk.KeyId = "test-key-id";

        // Current time values
        var now = _clock.UtcNow.ToUnixTimeSeconds();
        var exp = now + 3600; // Valid for 1 hour

        var attestationJwt = CreateAttestationJwt(
            typValue: "oauth-client-attestation+jwt",
            algValue: "RS256",
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
            signingKey: _defaultPrivateKey
        );

        var popJwt = CreatePopJwt(
            typValue: "oauth-client-attestation-pop+jwt",
            algValue: "RS256",
            issValue: ValidClientId,
            expValue: exp,
            iatValue: now,
            nbfValue: now,
            audValue: AuthServerIdentity,
            jtiValue: Guid.NewGuid().ToString(),
            nonceValue: "test-nonce",
            signingKey: _defaultPrivateKey
        );

        return (attestationJwt, popJwt);
    }

    private string CreateAttestationJwt(
        string typValue = "oauth-client-attestation+jwt",
        string algValue = "RS256",
        bool includeIss = true,
        string issValue = "https://client-issuer.example.com",
        string subValue = null,
        long? expValue = null,
        long? iatValue = null,
        long? nbfValue = null,
        string audValue = null,
        string cnfValue = null,
        SecurityKey signingKey = null)
    {
        subValue ??= ValidClientId;
        expValue ??= _clock.UtcNow.AddHours(1).ToUnixTimeSeconds();
        iatValue ??= _clock.UtcNow.ToUnixTimeSeconds();
        nbfValue ??= _clock.UtcNow.ToUnixTimeSeconds();
        audValue ??= AuthServerIdentity;
        signingKey ??= _defaultPrivateKey;

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
            // Create a default cnf with jwk
            var jwk = signingKey switch
            {
                RsaSecurityKey rsaKey => JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsaKey.Rsa.ExportParameters(false))),
                SymmetricSecurityKey symmetricKey => JsonWebKeyConverter.ConvertFromSymmetricSecurityKey(symmetricKey),
                _ => throw new NotSupportedException($"Algorithm {algValue} is not supported.")
            };
            jwk.KeyId = "test-key-id";
            payload["cnf"] = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "jwk", JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(jwk)) }
            });
        }

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(signingKey, algValue), header);

        return token;
    }

    private string CreatePopJwt(
        string typValue = "oauth-client-attestation-pop+jwt",
        string algValue = "RS256",
        string issValue = null,
        long? expValue = null,
        long? iatValue = null,
        long? nbfValue = null,
        string audValue = null,
        string jtiValue = null,
        string nonceValue = null,
        SecurityKey signingKey = null)
    {
        issValue ??= ValidClientId;
        expValue ??= _clock.UtcNow.AddHours(1).ToUnixTimeSeconds();
        iatValue ??= _clock.UtcNow.ToUnixTimeSeconds();
        nbfValue ??= _clock.UtcNow.ToUnixTimeSeconds();
        audValue ??= AuthServerIdentity;
        jtiValue ??= Guid.NewGuid().ToString();
        nonceValue ??= "test-nonce";
        signingKey ??= _defaultPrivateKey;

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
        var token = handler.CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(signingKey, algValue), header);

        return token;
    }
}
