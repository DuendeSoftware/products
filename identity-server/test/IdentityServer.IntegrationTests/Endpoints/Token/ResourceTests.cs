// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Token;

public class ResourceTests
{
    private const string Category = "Token endpoint";
    private const string client_id = "client";
    private const string client_secret = "secret";
    private const string username = "bob";
    private const string password = "password";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    public ResourceTests()
    {
        _mockPipeline.Clients.Add(new Client
        {
            ClientId = client_id,
            ClientSecrets = new List<Secret> { new Secret(client_secret.Sha256()) },
            AllowedGrantTypes = { GrantType.ClientCredentials, GrantType.ResourceOwnerPassword },
            AllowedScopes = new List<string> { "scope1", "scope2", "scope3", "scope4", },
            AllowOfflineAccess = true,
        });

        _mockPipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Password = "password",
            Claims = new Claim[]
            {
                new Claim("name", "Bob Loblaw"),
                new Claim("email", "bob@loblaw.com"),
                new Claim("role", "Attorney")
            }
        });

        _mockPipeline.ApiResources.AddRange(new ApiResource[] {
            new ApiResource("urn:api1"){ Scopes={ "scope1", "scope3" } },
            new ApiResource("urn:api2"){ Scopes={ "scope2" } },
            new ApiResource("urn:api3"){ Scopes={ "scope1", "scope3" }, RequireResourceIndicator = true },
            new ApiResource("urn:api4"){ RequireResourceIndicator = true },
        });

        _mockPipeline.ApiScopes.AddRange(new[] {
            new ApiScope("scope1"),
            new ApiScope("scope2"),
            new ApiScope("scope3"),
            new ApiScope("scope4"),
        });

        _mockPipeline.Initialize();
    }

    //////////////////////////////////////////////////////////////////
    //// helpers
    //////////////////////////////////////////////////////////////////
    private IEnumerable<Claim> ParseAccessTokenClaims(TokenResponse tokenResponse)
    {
        tokenResponse.IsError.ShouldBeFalse(tokenResponse.Error);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenResponse.AccessToken);
        return token.Claims;
    }
    //////////////////////////////////////////////////////////////////
    //////////////////////////////////////////////////////////////////

    [Fact]
    [Trait("Category", Category)]
    public async Task client_credentials_without_resource_without_scope_should_succeed()
    {
        var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = client_id,
            ClientSecret = client_secret,
        }, _ct);

        var claims = ParseAccessTokenClaims(tokenResponse);
        claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api1", "urn:api2"]);
        claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "scope2", "scope3", "scope4"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_credentials_with_resource_without_scope_should_succeed()
    {
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                Parameters =
                {
                    { "resource", "urn:api1" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api1"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "scope3"]);
        }
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                Parameters =
                {
                    { "resource", "urn:api2" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api2"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope2"]);
        }
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                Parameters =
                {
                    { "resource", "urn:api3" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api3"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "scope3"]);
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_credentials_without_resource_with_scope_should_succeed()
    {
        var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = client_id,
            ClientSecret = client_secret,
            Scope = "scope1",
        }, _ct);

        var claims = ParseAccessTokenClaims(tokenResponse);
        claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api1"]);
        claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_credentials_with_resource_with_scope_should_succeed()
    {
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                Scope = "scope1",
                Parameters =
                {
                    { "resource", "urn:api1" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api1"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1"]);
        }
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                Scope = "scope1",
                Parameters =
                {
                    { "resource", "urn:api3" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api3"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1"]);
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_credentials_with_invalid_resource_and_scope_should_fail()
    {
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                Scope = "scope4",
                Parameters =
                {
                    { "resource", "urn:api2" }
                }
            }, _ct);
            tokenResponse.IsError.ShouldBeTrue();
            tokenResponse.Error.ShouldBe("invalid_target");
        }
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                Scope = "scope2",
                Parameters =
                {
                    { "resource", "urn:api3" }
                }
            }, _ct);
            tokenResponse.IsError.ShouldBeTrue();
            tokenResponse.Error.ShouldBe("invalid_target");
        }
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                Scope = "scope4",
                Parameters =
                {
                    { "resource", "urn:api4" }
                }
            }, _ct);
            tokenResponse.IsError.ShouldBeTrue();
            tokenResponse.Error.ShouldBe("invalid_target");
        }
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                Parameters =
                {
                    { "resource", "urn:api4" }
                }
            }, _ct);

            tokenResponse.IsError.ShouldBeTrue();
            tokenResponse.Error.ShouldBe("invalid_target");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_credentials_with_empty_resource_should_be_treated_as_if_no_resource_and_succeed()
    {
        var tokenResponse = await _mockPipeline.BackChannelClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = client_id,
            ClientSecret = client_secret,
            Parameters =
            {
                { "resource", " " }
            }
        }, _ct);

        var claims = ParseAccessTokenClaims(tokenResponse);
        claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api1", "urn:api2"]);
        claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "scope2", "scope3", "scope4"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task refresh_token_requested_without_resource_without_scope_should_succeed()
    {
        var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = client_id,
            ClientSecret = client_secret,
            UserName = username,
            Password = password,
        }, _ct);

        var claims = ParseAccessTokenClaims(tokenResponse);
        claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api1", "urn:api2"]);
        claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "scope2", "scope3", "scope4", "offline_access"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task refresh_token_requested_with_resource_without_scope_should_succeed()
    {
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Parameters =
                {
                    { "resource", "urn:api1" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api1"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "scope3", "offline_access"]);
        }

        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Parameters =
                {
                    { "resource", "urn:api2" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api2"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope2", "offline_access"]);
        }

        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Parameters =
                {
                    { "resource", "urn:api3" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api3"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "scope3", "offline_access"]);
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task refresh_token_requested_without_resource_with_scope_should_succeed()
    {
        var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = client_id,
            ClientSecret = client_secret,
            UserName = username,
            Password = password,
            Scope = "scope1 offline_access"
        }, _ct);

        var claims = ParseAccessTokenClaims(tokenResponse);
        claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api1"]);
        claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "offline_access"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task refresh_token_requested_with_resource_with_scope_should_succeed()
    {
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Scope = "scope1 offline_access",
                Parameters =
                {
                    { "resource", "urn:api1" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api1"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "offline_access"]);
        }
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Scope = "scope1 offline_access",
                Parameters =
                {
                    { "resource", "urn:api3" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api3"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "offline_access"]);
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task refresh_token_requested_with_invalid_resource_and_scope_should_fail()
    {
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Scope = "scope4 offline_access",
                Parameters =
                {
                    { "resource", "urn:api2" }
                }
            }, _ct);
            tokenResponse.IsError.ShouldBeTrue();
            tokenResponse.Error.ShouldBe("invalid_target");
        }
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Scope = "scope2 offline_access",
                Parameters =
                {
                    { "resource", "urn:api3" }
                }
            }, _ct);
            tokenResponse.IsError.ShouldBeTrue();
            tokenResponse.Error.ShouldBe("invalid_target");
        }
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Scope = "scope4 offline_access",
                Parameters =
                {
                    { "resource", "urn:api4" }
                }
            }, _ct);
            tokenResponse.IsError.ShouldBeTrue();
            tokenResponse.Error.ShouldBe("invalid_target");
        }
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Scope = "offline_access",
                Parameters =
                {
                    { "resource", "urn:api4" }
                }
            }, _ct);

            tokenResponse.IsError.ShouldBeTrue();
            tokenResponse.Error.ShouldBe("invalid_target");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task refresh_token_exchange_with_resource_should_succeed()
    {
        var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = client_id,
            ClientSecret = client_secret,
            UserName = username,
            Password = password,
            Scope = "scope1 scope2 scope3 scope4 offline_access",
        }, _ct);

        {
            tokenResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                RefreshToken = tokenResponse.RefreshToken,
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api1", "urn:api2"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "scope2", "scope3", "scope4", "offline_access"]);
        }
        {
            tokenResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                RefreshToken = tokenResponse.RefreshToken,
                Parameters =
                {
                    { "resource", "urn:api1" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api1"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope1", "scope3", "offline_access"]);
        }
        {
            tokenResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                RefreshToken = tokenResponse.RefreshToken,
                Parameters =
                {
                    { "resource", "urn:api2" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(["urn:api2"]);
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(["scope2", "offline_access"]);
        }
        {
            tokenResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                RefreshToken = tokenResponse.RefreshToken,
                Parameters =
                {
                    { "resource", "urn:api3" }
                }
            }, _ct);

            var claims = ParseAccessTokenClaims(tokenResponse);
            claims.Where(x => x.Type == "aud").Select(x => x.Value).ShouldBe(new[] { "urn:api3" });
            claims.Where(x => x.Type == "scope").Select(x => x.Value).ShouldBe(new[] { "scope1", "scope3", "offline_access" });
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task refresh_token_exchange_with_invalid_resource_should_fail()
    {
        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Scope = "scope1 scope2 scope3 scope4 offline_access",
            }, _ct);
            tokenResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                RefreshToken = tokenResponse.RefreshToken,
                Parameters =
                {
                    { "resource", "urn:api4" }
                }
            }, _ct);
            tokenResponse.IsError.ShouldBeTrue();
            tokenResponse.Error.ShouldBe("invalid_target");
        }


        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Scope = "scope2 offline_access",
            }, _ct);
            tokenResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                RefreshToken = tokenResponse.RefreshToken,
                Parameters =
                {
                    { "resource", "urn:api1" }
                }
            }, _ct);
            tokenResponse.IsError.ShouldBeTrue();
            tokenResponse.Error.ShouldBe("invalid_target");
        }

        {
            var tokenResponse = await _mockPipeline.BackChannelClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                UserName = username,
                Password = password,
                Scope = "scope4 offline_access",
            }, _ct);
            tokenResponse = await _mockPipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client_id,
                ClientSecret = client_secret,
                RefreshToken = tokenResponse.RefreshToken,
                Parameters =
                {
                    { "resource", "urn:api1" }
                }
            }, _ct);
            tokenResponse.IsError.ShouldBeTrue();
            tokenResponse.Error.ShouldBe("invalid_target");
        }
    }
}
