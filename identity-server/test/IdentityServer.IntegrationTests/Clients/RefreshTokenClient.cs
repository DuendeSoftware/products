// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel.Client;
using FluentAssertions;
using IntegrationTests.Clients.Setup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace IntegrationTests.Clients;

public class RefreshTokenClient
{
    private const string TokenEndpoint = "https://server/connect/token";
    private const string RevocationEndpoint = "https://server/connect/revocation";

    private readonly HttpClient _client;

    public RefreshTokenClient()
    {
        var builder = new WebHostBuilder()
            .UseStartup<Startup>();
        var server = new TestServer(builder);

        _client = server.CreateClient();
    }

    [Fact]
    public async Task Requesting_a_refresh_token_without_identity_scopes_should_return_expected_results()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "api1 offline_access",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().NotBeNull();

        response = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            RefreshToken = response.RefreshToken
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().NotBeNull();
    }

    [Fact]
    public async Task Requesting_a_refresh_token_with_identity_scopes_should_return_expected_results()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "openid api1 offline_access",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().NotBeNull();

        response = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            RefreshToken = response.RefreshToken
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().NotBeNull();
        response.RefreshToken.Should().NotBeNull();
    }

    [Fact]
    public async Task Refreshing_a_refresh_token_with_reuse_should_return_same_refresh_token()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient.reuse",
            ClientSecret = "secret",

            Scope = "openid api1 offline_access",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().NotBeNull();

        var rt1 = response.RefreshToken;

        response = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient.reuse",
            ClientSecret = "secret",

            RefreshToken = response.RefreshToken
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().NotBeNull();
        response.RefreshToken.Should().NotBeNull();

        var rt2 = response.RefreshToken;

        rt1.Should().BeEquivalentTo(rt2);
    }

    [Fact]
    public async Task Refreshing_a_refresh_token_with_one_time_only_should_return_different_refresh_token()
    {
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "openid api1 offline_access",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().NotBeNull();

        var rt1 = response.RefreshToken;

        response = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            RefreshToken = response.RefreshToken
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().NotBeNull();
        response.RefreshToken.Should().NotBeNull();

        var rt2 = response.RefreshToken;

        rt1.Should().NotBeEquivalentTo(rt2);
    }

    [Fact]
    public async Task Replaying_a_rotated_token_should_fail()
    {
        // request initial token
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "openid api1 offline_access",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().NotBeNull();

        var rt1 = response.RefreshToken;

        // refresh token
        response = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            RefreshToken = response.RefreshToken
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().NotBeNull();
        response.RefreshToken.Should().NotBeNull();

        // refresh token (again)
        response = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            RefreshToken = rt1
        });

        response.IsError.Should().BeTrue();
        response.Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task Using_a_valid_refresh_token_should_succeed()
    {
        // request initial token
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "openid api1 offline_access",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().NotBeNull();

        var rt1 = response.RefreshToken;

        // refresh token
        response = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            RefreshToken = rt1
        });

        response.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Using_a_revoked_refresh_token_should_fail()
    {
        // request initial token
        var response = await _client.RequestPasswordTokenAsync(new PasswordTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            Scope = "openid api1 offline_access",
            UserName = "bob",
            Password = "bob"
        });

        response.IsError.Should().BeFalse();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");
        response.IdentityToken.Should().BeNull();
        response.RefreshToken.Should().NotBeNull();

        var rt1 = response.RefreshToken;

        // revoke refresh token
        var revocationResponse = await _client.RevokeTokenAsync(new TokenRevocationRequest
        {
            Address = RevocationEndpoint,

            ClientId = "roclient",
            ClientSecret = "secret",

            Token = rt1,
            TokenTypeHint = "refresh_token"
        });

        revocationResponse.IsError.Should().Be(false);

        // refresh token
        response = await _client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = TokenEndpoint,
            ClientId = "roclient",
            ClientSecret = "secret",

            RefreshToken = rt1
        });

        response.IsError.Should().BeTrue();
        response.Error.Should().Be("invalid_grant");
    }
}
