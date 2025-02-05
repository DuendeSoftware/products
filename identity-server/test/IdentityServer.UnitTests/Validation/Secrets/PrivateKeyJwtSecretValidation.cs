// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Shouldly;
using Duende.IdentityModel;
using UnitTests.Common;
using UnitTests.Services.Default;
using UnitTests.Validation.Setup;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

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

    private JwtSecurityToken CreateToken(string clientId, string aud = "https://idsrv.com/", DateTime? nowOverride = null)
    {
        return CreateTokenHelper(clientId, new Claim(JwtClaimTypes.Audience, aud), nowOverride);
    }

    private JwtSecurityToken CreateToken(string clientId, string[] audiences, DateTime? nowOverride = null)
    {
        return CreateTokenHelper(clientId, new Claim(JwtClaimTypes.Audience, JsonSerializer.Serialize(audiences), JsonClaimValueTypes.JsonArray), nowOverride);
    }
    
    private JwtSecurityToken CreateTokenHelper(string clientId,  Claim aud = null, DateTime? nowOverride = null)
    {
        var certificate = TestCert.Load();
        var now = nowOverride ?? DateTime.UtcNow;
        aud ??= new Claim("aud", "https://idsrv.com");
        
        var token = new JwtSecurityToken(
            clientId,
            null,
            new List<Claim>()
            {
                new("jti", Guid.NewGuid().ToString()),
                new(JwtClaimTypes.Subject, clientId),
                new(JwtClaimTypes.IssuedAt, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                aud
            },
            now,
            now.AddMinutes(1),
            new SigningCredentials(
                new X509SecurityKey(certificate),
                SecurityAlgorithms.RsaSha256
            )
        );
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
    [InlineData("https://idsrv.com")]
    [InlineData("https://idsrv.com/")]
    [InlineData("https://idsrv.com/connect/token")]
    [InlineData("https://idsrv.com/connect/ciba")]
    [InlineData("https://idsrv.com/connect/par")]
    public async Task Valid_legacy_aud(string aud)
    {
        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId, aud:aud)),
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
    public async Task Valid_strict_aud(string aud, bool expectSuccess)
    {
        _options.StrictClientAssertionAudienceValidation = true;
        
        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId, aud:aud)), 
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.ShouldBe(expectSuccess, result.Error);
    }

    [Theory]
    [InlineData("https://idsrv.com")]
    [InlineData("https://idsrv.com/")]
    [InlineData("https://idsrv.com/connect/token")]
    [InlineData("https://idsrv.com/connect/ciba")]
    [InlineData("https://idsrv.com/connect/par")]
    public async Task StrictAudience_does_not_allow_single_valued_arrays(string aud)
    {
        _options.StrictClientAssertionAudienceValidation = true;
        
        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);
        
        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId, [aud])),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };
    
        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);
    
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task StrictAudience_does_not_allow_multi_valued_arrays()
    {
        _options.StrictClientAssertionAudienceValidation = true;
        
        var clientId = "certificate_base64_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);
        
        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = new JwtSecurityTokenHandler().WriteToken(CreateToken(clientId, 
                ["https://idsrv.com", "https://idsrv.com/"])),
            Type = IdentityServerConstants.ParsedSecretTypes.JwtBearer
        };
    
        var result = await _validator.ValidateAsync(client.ClientSecrets, secret);
    
        result.Success.ShouldBeFalse();
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
}