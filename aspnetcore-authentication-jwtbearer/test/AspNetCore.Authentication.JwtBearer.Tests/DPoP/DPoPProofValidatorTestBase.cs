// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duende.AspNetCore.Authentication.JwtBearer.DPoP.TestFramework;
using Duende.IdentityModel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public abstract class DPoPProofValidatorTestBase
{
    public DPoPProofValidatorTestBase()
    {
        ExpirationLogger = new FakeLogger<DPoPExpirationValidator>();
        Clock = new FakeTimeProvider();
        DataProtectionProvider = new EphemeralDataProtectionProvider();
        OptionsMonitor = new TestOptionsMonitor<DPoPOptions>(Options);
        ExpirationValidator = new DPoPExpirationValidator(
            Clock,
            ExpirationLogger);
        NonceValidator = new DefaultDPoPNonceValidator(
            OptionsMonitor,
            DataProtectionProvider,
            Clock,
            new FakeLogger<DefaultDPoPNonceValidator>(),
            ExpirationValidator);
        ProofValidator = new(
            OptionsMonitor,
            NonceValidator,
            ReplayCache,
            Clock,
            new FakeLogger<DPoPProofValidator>(),
            ExpirationValidator);
        var jtiBytes = Encoding.UTF8.GetBytes(TokenId);
        TokenIdHash = Base64Url.EncodeToString(SHA256.HashData(jtiBytes));
        Context = new()
        {
            Options = Options,
            Scheme = "test-auth-scheme",
            ExpectedMethod = HttpMethod,
            AccessToken = AccessToken,
            ProofToken = CreateDPoPProofToken(),
            ExpectedUrl = HttpUrl
        };
    }

    internal FakeLogger<DPoPExpirationValidator> ExpirationLogger { get; }
    protected FakeTimeProvider Clock { get; }
    protected IDataProtectionProvider DataProtectionProvider { get; }
    protected IDataProtector DataProtector => NonceValidator.DataProtector;
    protected DPoPOptions Options = new();
    protected TestOptionsMonitor<DPoPOptions> OptionsMonitor { get; }
    internal DPoPExpirationValidator ExpirationValidator { get; }
    internal DefaultDPoPNonceValidator NonceValidator { get; }
    internal DPoPProofValidator ProofValidator { get; }

    protected DPoPProofValidationContext Context;
    protected TestReplayCache ReplayCache = new();


    protected DPoPProofValidationResult Result = new();

    // This is just an arbitrary date that we're going to do all our date arithmetic relative to.
    // It was chosen because it is convenient to use - it is well within the range of DateTime
    protected const long IssuedAt = 1704088800; // Mon Jan 01 2024 06:00:00 GMT+0000
    protected const long ValidFor = 100;
    protected const long ClockSkew = 10;
    protected const string AccessToken = "test-access-token";
    protected const string AccessTokenHash = "WXSA1LYsphIZPxnnP-TMOtF_C_nPwWp8v0tQZBMcSAU"; // Pre-computed sha256 hash of "test-access-token"

    protected const string PrivateRsaJwk =
    """
    {
        "D":"QeBWodq0hSYjfAxxo0VZleXLqwwZZeNWvvFfES4WyItao_-OJv1wKA7zfkZxbWkpK5iRbKrl2AMJ52AtUo5JJ6QZ7IjAQlgM0lBg3ltjb1aA0gBsK5XbiXcsV8DiAnRuy6-XgjAKPR8Lo-wZl_fdPbVoAmpSdmfn_6QXXPBai5i7FiyDbQa16pI6DL-5SCj7F78QDTRiJOqn5ElNvtoJEfJBm13giRdqeriFi3pCWo7H3QBgTEWtDNk509z4w4t64B2HTXnM0xj9zLnS42l7YplJC7MRibD4nVBMtzfwtGRKLj8beuDgtW9pDlQqf7RVWX5pHQgiHAZmUi85TEbYdQ",
        "DP":"h2F54OMaC9qq1yqR2b55QNNaChyGtvmTHSdqZJ8lJFqvUorlz-Uocj2BTowWQnaMd8zRKMdKlSeUuSv4Z6WmjSxSsNbonI6_II5XlZLWYqFdmqDS-xCmJY32voT5Wn7OwB9xj1msDqrFPg-PqSBOh5OppjCqXqDFcNvSkQSajXc",
        "DQ":"VABdS20Nxkmq6JWLQj7OjRxVJuYsHrfmWJmDA7_SYtlXaPUcg-GiHGQtzdDWEeEi0dlJjv9I3FdjKGC7CGwqtVygW38DzVYJsV2EmRNJc1-j-1dRs_pK9GWR4NYm0mVz_IhS8etIf9cfRJk90xU3AL3_J6p5WNF7I5ctkLpnt8M",
        "E":"AQAB",
        "Kty":"RSA",
        "N":"yWWAOSV3Z_BW9rJEFvbZyeU-q2mJWC0l8WiHNqwVVf7qXYgm9hJC0j1aPHku_Wpl38DpK3Xu3LjWOFG9OrCqga5Pzce3DDJKI903GNqz5wphJFqweoBFKOjj1wegymvySsLoPqqDNVYTKp4nVnECZS4axZJoNt2l1S1bC8JryaNze2stjW60QT-mIAGq9konKKN3URQ12dr478m0Oh-4WWOiY4HrXoSOklFmzK-aQx1JV_SZ04eIGfSw1pZZyqTaB1BwBotiy-QA03IRxwIXQ7BSx5EaxC5uMCMbzmbvJqjt-q8Y1wyl-UQjRucgp7hkfHSE1QT3zEex2Q3NFux7SQ",
        "P":"_T7MTkeOh5QyqlYCtLQ2RWf2dAJ9i3wrCx4nEDm1c1biijhtVTL7uJTLxwQIM9O2PvOi5Dq-UiGy6rhHZqf5akWTeHtaNyI-2XslQfaS3ctRgmGtRQL_VihK-R9AQtDx4eWL4h-bDJxPaxby_cVo_j2MX5AeoC1kNmcCdDf_X0M",
        "Q":"y5ZSThaGLjaPj8Mk2nuD8TiC-sb4aAZVh9K-W4kwaWKfDNoPcNb_dephBNMnOp9M1br6rDbyG7P-Sy_LOOsKg3Q0wHqv4hnzGaOQFeMJH4HkXYdENC7B5JG9PefbC6zwcgZWiBnsxgKpScNWuzGF8x2CC-MdsQ1bkQeTPbJklIM",
        "QI":"i716Vt9II_Rt6qnjsEhfE4bej52QFG9a1hSnx5PDNvRrNqR_RpTA0lO9qeXSZYGHTW_b6ZXdh_0EUwRDEDHmaxjkIcTADq6JLuDltOhZuhLUSc5NCKLAVCZlPcaSzv8-bZm57mVcIpx0KyFHxvk50___Jgx1qyzwLX03mPGUbDQ"
    }
    """;

    protected const string PublicRsaJwk =
    """
    {
        "kty":"RSA",
        "use":"sig",
        "e":"AQAB",
        "n":"yWWAOSV3Z_BW9rJEFvbZyeU-q2mJWC0l8WiHNqwVVf7qXYgm9hJC0j1aPHku_Wpl38DpK3Xu3LjWOFG9OrCqga5Pzce3DDJKI903GNqz5wphJFqweoBFKOjj1wegymvySsLoPqqDNVYTKp4nVnECZS4axZJoNt2l1S1bC8JryaNze2stjW60QT-mIAGq9konKKN3URQ12dr478m0Oh-4WWOiY4HrXoSOklFmzK-aQx1JV_SZ04eIGfSw1pZZyqTaB1BwBotiy-QA03IRxwIXQ7BSx5EaxC5uMCMbzmbvJqjt-q8Y1wyl-UQjRucgp7hkfHSE1QT3zEex2Q3NFux7SQ"
    }
    """;

    protected static readonly Dictionary<string, string> PublicRsaJwkDeserialized = JsonSerializer.Deserialize<Dictionary<string, string>>(PublicRsaJwk)!;

    protected const string PrivateEcdsaJwk =
    """
    {
        "alg": "ES256",
        "crv": "P-256",
        "d": "9CRuA1-1ATel3-CvNg7cT-l-WN8o6KPTvEMqMxhLhVI",
        "ext": true,
        "kid": "7exUU3NSbzLfBTLciHM_IJPKfa9sBCMaD-FdZ70jBGs",
        "kty": "EC",
        "x": "md6SP5IyW7kqjwqNS3fekeF-uXLz4iMwmm1tDjtZq1w",
        "y": "uHzp1K3vnrqoVUwZ_7v3wxAr1reHPdkGoDGzH_pT0ak"
    }
    """;

    protected const string PublicEcdsaJwk =
    """
    {
        "alg": "ES256",
        "crv": "P-256",
        "ext": true,
        "kid": "7exUU3NSbzLfBTLciHM_IJPKfa9sBCMaD-FdZ70jBGs",
        "kty": "EC",
        "x": "md6SP5IyW7kqjwqNS3fekeF-uXLz4iMwmm1tDjtZq1w",
        "y": "uHzp1K3vnrqoVUwZ_7v3wxAr1reHPdkGoDGzH_pT0ak"
    }
    """;

    protected static readonly Dictionary<string, object> PublicEcdsaJwkDeserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(PublicEcdsaJwk)!;

    protected static readonly byte[] PrivateHmacKey = CreateHmacKey();

    private static byte[] CreateHmacKey()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        return randomBytes;
    }

    protected const string TokenId = "test-token-jti";
    protected readonly string TokenIdHash;
    protected const string HttpMethod = "GET";
    protected const string HttpUrl = "https://example.com";

    protected static string CreateDPoPProofToken(
        string typ = "dpop+jwt",
        string alg = SecurityAlgorithms.RsaSha256,
        object? jwk = null,
        string? jti = null,
        string? htm = null,
        string? htu = null,
        string? ath = null)
    {
        var tokenHandler = new JsonWebTokenHandler();

        var claims = new List<Claim>();
        if (jti != null)
        {
            claims.Add(new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()));
        }

        if (htm != null)
        {
            claims.Add(new Claim(JwtClaimTypes.DPoPHttpMethod, htm));
        }

        if (htu != null)
        {
            claims.Add(new Claim(JwtClaimTypes.DPoPHttpUrl, htu));
        }

        if (ath != null)
        {
            claims.Add(new Claim(JwtClaimTypes.DPoPHttpUrl, ath));
        }

        var creds = alg switch
        {
            string s when s.StartsWith("ES") => new SigningCredentials(new JsonWebKey(PrivateEcdsaJwk), alg),
            string s when s.StartsWith("RS") || s.StartsWith("PS") => new SigningCredentials(new JsonWebKey(PrivateRsaJwk), alg),
            string s when s.StartsWith("HS") => new SigningCredentials(new SymmetricSecurityKey(PrivateHmacKey), alg),
            "none" => null,
            _ => throw new ArgumentException("alg value not mocked")
        };

        var jwkPayload = jwk ?? alg switch
        {

            string s when s.StartsWith("ES") => PublicEcdsaJwkDeserialized,
            string s when s.StartsWith("RS") || s.StartsWith("PS") => PublicRsaJwkDeserialized,
            _ => "null"
        };


        var d = new SecurityTokenDescriptor
        {
            TokenType = typ,
            IssuedAt = DateTime.UtcNow,
            AdditionalHeaderClaims = new Dictionary<string, object>
            {
                { JwtClaimTypes.JsonWebKey, jwkPayload },
            },
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = creds
        };
        return tokenHandler.CreateToken(d);
    }

    protected Claim CnfClaim(string jwkString)
    {
        jwkString ??= PublicRsaJwk;
        var jwk = new JsonWebKey(jwkString);
        var cnf = jwk.CreateThumbprintCnf();
        return new Claim(JwtClaimTypes.Confirmation, cnf);
    }
}
