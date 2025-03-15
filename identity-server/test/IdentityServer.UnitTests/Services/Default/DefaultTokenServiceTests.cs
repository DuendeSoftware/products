// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class DefaultTokenServiceTests
{
    private DefaultTokenService _subject;

    private MockClaimsService _mockClaimsService = new MockClaimsService();
    private MockReferenceTokenStore _mockReferenceTokenStore = new MockReferenceTokenStore();
    private MockTokenCreationService _mockTokenCreationService = new MockTokenCreationService();
    private MockSystemClock _mockSystemClock = new MockSystemClock();
    private MockKeyMaterialService _mockKeyMaterialService = new MockKeyMaterialService();
    private IdentityServerOptions _options = new IdentityServerOptions();

    public DefaultTokenServiceTests()
    {
        _options.IssuerUri = "https://test.identityserver.io";

        var svcs = new ServiceCollection();
        svcs.AddSingleton(_options);

        _subject = new DefaultTokenService(
            _mockClaimsService,
            _mockReferenceTokenStore,
            _mockTokenCreationService,
            _mockSystemClock,
            _mockKeyMaterialService,
            _options,
            TestLogger.Create<DefaultTokenService>());
    }

    [Fact]
    public async Task CreateAccessTokenAsync_should_include_aud_for_each_ApiResource()
    {
        var request = new TokenCreationRequest
        {
            ValidatedResources = new ResourceValidationResult()
            {
                Resources = new Resources()
                {
                    ApiResources =
                    {
                        new ApiResource("api1"){ Scopes = { "scope1" } },
                        new ApiResource("api2"){ Scopes = { "scope2" } },
                        new ApiResource("api3"){ Scopes = { "scope3" } },
                    },
                },
                ParsedScopes =
                {
                    new ParsedScopeValue("scope1"),
                    new ParsedScopeValue("scope2"),
                    new ParsedScopeValue("scope3"),
                }
            },
            ValidatedRequest = new ValidatedRequest()
            {
                Client = new Client { }
            }
        };

        var result = await _subject.CreateAccessTokenAsync(request);

        result.Audiences.Count.ShouldBe(3);
        result.Audiences.ShouldBe(["api1", "api2", "api3"]);
    }

    [Fact]
    public async Task CreateAccessTokenAsync_when_no_apiresources_should_not_include_any_aud()
    {
        var request = new TokenCreationRequest
        {
            ValidatedResources = new ResourceValidationResult()
            {
                Resources = new Resources()
                {
                    ApiScopes =
                    {
                        new ApiScope("scope1"),
                        new ApiScope("scope2"),
                        new ApiScope("scope3"),
                    },
                },
                ParsedScopes =
                {
                    new ParsedScopeValue("scope1"),
                    new ParsedScopeValue("scope2"),
                    new ParsedScopeValue("scope3"),
                }
            },
            ValidatedRequest = new ValidatedRequest()
            {
                Client = new Client { }
            }
        };

        var result = await _subject.CreateAccessTokenAsync(request);

        result.Audiences.Count.ShouldBe(0);
    }

    [Fact]
    public async Task CreateAccessTokenAsync_when_no_session_should_not_include_sid()
    {
        var request = new TokenCreationRequest
        {
            ValidatedResources = new ResourceValidationResult(),
            ValidatedRequest = new ValidatedRequest()
            {
                Client = new Client { },
                SessionId = null
            }
        };

        var result = await _subject.CreateAccessTokenAsync(request);

        result.Claims.SingleOrDefault(x => x.Type == JwtClaimTypes.SessionId).ShouldBeNull();
    }

    [Fact]
    public async Task CreateAccessTokenAsync_when_session_should_include_sid()
    {
        var request = new TokenCreationRequest
        {
            ValidatedResources = new ResourceValidationResult(),
            ValidatedRequest = new ValidatedRequest()
            {
                Client = new Client { },
                SessionId = "123"
            }
        };

        var result = await _subject.CreateAccessTokenAsync(request);

        result.Claims.SingleOrDefault(x => x.Type == JwtClaimTypes.SessionId).Value.ShouldBe("123");
    }

    [Fact]
    public async Task CreateSecurityTokenAsync_should_include_jti_in_access_tokens()
    {
        var token = new Token
        {
            Claims = { new Claim("sub", "123") }
        };

        {
            token.IncludeJwtId = false;
            token.Type = OidcConstants.TokenTypes.IdentityToken;
            var result = await _subject.CreateSecurityTokenAsync(token);
            _mockTokenCreationService.Token.Claims.ShouldNotContain(x => x.Type == "jti");
        }

        {
            token.IncludeJwtId = false;
            token.Type = OidcConstants.TokenTypes.AccessToken;
            var result = await _subject.CreateSecurityTokenAsync(token);
            _mockTokenCreationService.Token.Claims.ShouldNotContain(x => x.Type == "jti");
        }

        {
            token.IncludeJwtId = true;
            token.Type = OidcConstants.TokenTypes.IdentityToken;
            var result = await _subject.CreateSecurityTokenAsync(token);
            _mockTokenCreationService.Token.Claims.ShouldNotContain(x => x.Type == "jti");
        }

        {
            token.IncludeJwtId = true;
            token.Type = OidcConstants.TokenTypes.AccessToken;
            var result = await _subject.CreateSecurityTokenAsync(token);
            _mockTokenCreationService.Token.Claims.ShouldContain(x => x.Type == "jti");
        }
    }
    [Fact]
    public async Task CreateSecurityTokenAsync_should_include_jti_access_tokens_for_older_versions()
    {
        var token = new Token
        {
            Claims =
            {
                new Claim("sub", "123")
            },
            Version = 4,
            Type = OidcConstants.TokenTypes.AccessToken,
            IncludeJwtId = false,
        };

        {
            var result = await _subject.CreateSecurityTokenAsync(token);
            _mockTokenCreationService.Token.Claims.ShouldNotContain(x => x.Type == "jti");
        }

        {
            token.Claims.Add(new Claim("jti", "xoxo"));
            token.Type = OidcConstants.TokenTypes.AccessToken;
            var result = await _subject.CreateSecurityTokenAsync(token);
            _mockTokenCreationService.Token.Claims.ShouldContain(x => x.Type == "jti");
            _mockTokenCreationService.Token.Claims.Single(x => x.Type == "jti").Value.ShouldNotBe("xoxo");
        }
    }
}
