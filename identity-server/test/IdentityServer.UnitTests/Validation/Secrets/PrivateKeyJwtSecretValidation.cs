// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using UnitTests.Common;
using UnitTests.Services.Default;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.Secrets;

public class PrivateKeyJwtSecretValidation
{
    private readonly ISecretValidator _validator;
    private readonly IClientStore _clients;
    private readonly IdentityServerOptions _options;

    public PrivateKeyJwtSecretValidation()
    {
        _options = new IdentityServerOptions();

        _validator = new PrivateKeyJwtSecretValidator(
            new TestIssuerNameService("https://idsrv.com"),
            new DefaultReplayCache(new TestCache()),
            new MockServerUrls() { Origin = "https://idsrv.com" },
            _options,
            new LoggerFactory().CreateLogger<PrivateKeyJwtSecretValidator>());

        _clients = new InMemoryClientStore(ClientValidationTestClients.Get());
    }

    private JwtSecurityToken CreateToken(string clientId, string aud = "https://idsrv.com/", DateTime? nowOverride = null, Typ typ = Typ.None) =>
        CreateTokenHelper(clientId, new Claim(JwtClaimTypes.Audience, aud), nowOverride, typ);

    private JwtSecurityToken CreateToken(string clientId, string[] audiences, DateTime? nowOverride = null, Typ typ = Typ.None) =>
        CreateTokenHelper(clientId, new Claim(JwtClaimTypes.Audience, JsonSerializer.Serialize(audiences), JsonClaimValueTypes.JsonArray), nowOverride, typ);

    public enum Typ
    {
        None,
        JWT,
        ClientAuthentication
    }

    private JwtSecurityToken CreateTokenHelper(string clientId, Claim aud = null, DateTime? nowOverride = null, Typ jwtTyp = Typ.None)
    {
        var certificate = TestCert.Load();
        var now = nowOverride ?? DateTime.UtcNow;
        aud ??= new Claim("aud", "https://idsrv.com");

        var handler = new JwtSecurityTokenHandler();

        var token = new JwtSecurityToken(
            issuer: clientId,
            audience: null,
            claims: new List<Claim>()
            {
                new("jti", Guid.NewGuid().ToString()),
                new(JwtClaimTypes.Subject, clientId),
                new(JwtClaimTypes.IssuedAt, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                aud
            },
            notBefore: now,
            expires: now.AddMinutes(1),
            signingCredentials: new SigningCredentials(
                new X509SecurityKey(certificate),
                SecurityAlgorithms.RsaSha256
            )
        );

        if (jwtTyp == Typ.ClientAuthentication)
        {
            token.Header["typ"] = "client-authentication+jwt";
        }
        else if (jwtTyp == Typ.JWT)
        {
            token.Header["typ"] = "JWT";
        }
        else
        {
            token.Header.Remove("typ");
        }

        return token;
    }

    [Fact]
    public async Task Invalid_Certificate_X5t_Only_Requires_Full_Certificate()
    {
        var clientId = "certificate_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var token = CreateToken(clientId);
        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(token),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Certificate_Thumbprint()
    {
        var clientId = "certificate_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId)),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Valid_Certificate_Base64()
    {
        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId)),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBeTrue();
    }

    [Theory]
    [InlineData("https://idsrv.com", Typ.None)]
    [InlineData("https://idsrv.com/", Typ.None)]
    [InlineData("https://idsrv.com/connect/token", Typ.None)]
    [InlineData("https://idsrv.com/connect/ciba", Typ.None)]
    [InlineData("https://idsrv.com/connect/par", Typ.None)]
    [InlineData("https://idsrv.com", Typ.JWT)]
    [InlineData("https://idsrv.com/", Typ.JWT)]
    [InlineData("https://idsrv.com/connect/token", Typ.JWT)]
    [InlineData("https://idsrv.com/connect/ciba", Typ.JWT)]
    [InlineData("https://idsrv.com/connect/par", Typ.JWT)]
    public async Task Strict_audience_disabled_and_no_typ_header_or_JWT_typ_header_allows_legacy_aud_values(string aud, Typ typ)
    {
        _options.Preview.StrictClientAssertionAudienceValidation = false;

        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId, aud: aud, typ: typ)),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBeTrue();
    }

    [Theory]
    [InlineData("https://idsrv.com", true)]
    [InlineData("https://idsrv.com/", true)]
    [InlineData("https://idsrv.com/connect/token", false)]
    [InlineData("https://idsrv.com/connect/ciba", false)]
    [InlineData("", false)]
    [InlineData("https://idsrv.com/connect/par", false)]
    public async Task Strict_audience_from_options_validates_audience(string aud, bool expectSuccess)
    {
        _options.Preview.StrictClientAssertionAudienceValidation = true;

        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId, aud: aud, typ: Typ.ClientAuthentication)),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBe(expectSuccess, result.Error);
    }

    [Theory]
    [InlineData("https://idsrv.com", true)]
    [InlineData("https://idsrv.com/", true)]
    [InlineData("https://idsrv.com/connect/token", false)]
    [InlineData("https://idsrv.com/connect/ciba", false)]
    [InlineData("", false)]
    [InlineData("https://idsrv.com/connect/par", false)]
    public async Task Strict_audience_from_typ_header_validates_audience(string aud, bool expectSuccess)
    {
        _options.Preview.StrictClientAssertionAudienceValidation = false;

        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId, aud: aud, typ: Typ.ClientAuthentication)),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBe(expectSuccess, result.Error);
    }

    [Theory]
    [InlineData(Typ.ClientAuthentication, true, false)]
    [InlineData(Typ.ClientAuthentication, false, false)]
    [InlineData(Typ.None, true, false)]
    [InlineData(Typ.None, false, true)]
    [InlineData(Typ.JWT, true, false)]
    [InlineData(Typ.JWT, false, true)]
    public async Task Strict_audience_does_not_allow_single_valued_arrays(Typ typ, bool setStrictOption, bool expectedResult)
    {
        _options.Preview.StrictClientAssertionAudienceValidation = setStrictOption;

        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);
        var token = new JwtSecurityTokenHandler().WriteToken(CreateToken(
            clientId,
            audiences: ["https://idsrv.com/connect/token"],
            typ: typ));

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = token,
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBe(expectedResult);
    }

    [Theory]
    [InlineData(Typ.ClientAuthentication, true, false)]
    [InlineData(Typ.ClientAuthentication, false, false)]
    [InlineData(Typ.None, true, false)]
    [InlineData(Typ.None, false, true)]
    [InlineData(Typ.JWT, true, false)]
    [InlineData(Typ.JWT, false, true)]
    public async Task Strict_audience_does_not_allow_multi_valued_arrays(Typ typ, bool setStrictOption, bool expectedResult)
    {
        _options.Preview.StrictClientAssertionAudienceValidation = setStrictOption;

        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);
        var token = new JwtSecurityTokenHandler().WriteToken(CreateToken(
            clientId,
            audiences: ["https://idsrv.com", "https://idsrv.com/"],
            typ: typ));

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = token,
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBe(expectedResult);
    }

    [Theory]
    [InlineData(Typ.ClientAuthentication, true, true)]
    [InlineData(Typ.ClientAuthentication, false, true)]
    [InlineData(Typ.None, true, false)]
    [InlineData(Typ.None, false, true)]
    [InlineData(Typ.JWT, true, false)]
    [InlineData(Typ.JWT, false, true)]
    public async Task Strict_audience_only_allows_correct_type(Typ typ, bool enforceStrict, bool expectedResult)
    {
        _options.Preview.StrictClientAssertionAudienceValidation = enforceStrict;

        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);
        var token = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId, typ: typ));

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = token,
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);
        result.Success.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task Invalid_Replay()
    {
        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);
        var token = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId));
        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = token,
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);
        result.Success.ShouldBeTrue();

        result = await _validator.ValidateAsync(client.ClientSecrets, secret);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Certificate_Base64()
    {
        var clientId = "certificate_base64_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId)),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Issuer()
    {
        var clientId = "certificate_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var token = CreateToken(clientId);
        token.Payload.Remove(JwtClaimTypes.Issuer);
        token.Payload.Add(JwtClaimTypes.Issuer, "invalid");
        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(token),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Subject()
    {
        var clientId = "certificate_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var token = CreateToken(clientId);
        token.Payload.Remove(JwtClaimTypes.Subject);
        token.Payload.Add(JwtClaimTypes.Subject, "invalid");
        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(token),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Expired_Token()
    {
        var clientId = "certificate_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var token = CreateToken(clientId, nowOverride: DateTime.UtcNow.AddHours(-1));
        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(token),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Unsigned_Token()
    {
        var clientId = "certificate_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var token = CreateToken(clientId);
        token.Header.Remove("alg");
        token.Header.Add("alg", "none");
        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(token),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_Not_Yet_Valid_Token()
    {
        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var token = CreateToken(clientId, nowOverride: DateTime.UtcNow.AddSeconds(30));
        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(token),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        _options.JwtValidationClockSkew = TimeSpan.FromSeconds(5);

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Signing_Algorithm_Not_Allowed_By_Configuration()
    {
        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var token = CreateToken(clientId);
        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(token),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        _options.SupportedClientAssertionSigningAlgorithms = ["Test"];

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBeFalse();
    }
}
