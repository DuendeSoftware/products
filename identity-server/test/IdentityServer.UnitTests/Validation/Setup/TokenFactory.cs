// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using UnitTests.Common;

namespace UnitTests.Validation.Setup;

internal static class TokenFactory
{
    public static Token CreateAccessToken(Client client, string subjectId, int lifetime, params string[] scopes)
    {
        var claims = new List<Claim>
        {
            new Claim("client_id", client.ClientId),
            new Claim("sub", subjectId)
        };

        scopes.ToList().ForEach(s => claims.Add(new Claim("scope", s)));

        var token = new Token(OidcConstants.TokenTypes.AccessToken)
        {
            CreationTime = DateTime.UtcNow,
            Audiences = { "https://idsvr.com/resources" },
            Issuer = "https://idsvr.com",
            Lifetime = lifetime,
            Claims = claims,
            ClientId = client.ClientId,
            AccessTokenType = client.AccessTokenType
        };

        return token;
    }

    public static Token CreateAccessTokenLong(Client client, string subjectId, int lifetime, int count, params string[] scopes)
    {
        var claims = new List<Claim>
        {
            new Claim("client_id", client.ClientId),
            new Claim("sub", subjectId)
        };

        for (var i = 0; i < count; i++)
        {
            claims.Add(new Claim("junk", "x".Repeat(100)));
        }

        scopes.ToList().ForEach(s => claims.Add(new Claim("scope", s)));

        var token = new Token(OidcConstants.TokenTypes.AccessToken)
        {
            CreationTime = DateTime.UtcNow,
            Audiences = { "https://idsvr.com/resources" },
            Issuer = "https://idsvr.com",
            Lifetime = lifetime,
            Claims = claims,
            ClientId = client.ClientId,
            AccessTokenType = client.AccessTokenType
        };

        return token;
    }

    public static Token CreateIdentityToken(string clientId, string subjectId)
    {
        var clients = Factory.CreateClientStore();

        var claims = new List<Claim>
        {
            new Claim("sub", subjectId)
        };

        var token = new Token(OidcConstants.TokenTypes.IdentityToken)
        {
            CreationTime = DateTime.UtcNow,
            Audiences = { clientId },
            ClientId = clientId,
            Issuer = "https://idsvr.com",
            Lifetime = 600,
            Claims = claims
        };

        return token;
    }

    public static Token CreateIdentityTokenLong(string clientId, string subjectId, int count)
    {
        var clients = Factory.CreateClientStore();

        var claims = new List<Claim>
        {
            new Claim("sub", subjectId)
        };

        for (var i = 0; i < count; i++)
        {
            claims.Add(new Claim("junk", "x".Repeat(100)));
        }

        var token = new Token(OidcConstants.TokenTypes.IdentityToken)
        {
            CreationTime = DateTime.UtcNow,
            Audiences = { clientId },
            ClientId = clientId,
            Issuer = "https://idsvr.com",
            Lifetime = 600,
            Claims = claims
        };

        return token;
    }
}
