// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using UnitTests.Common;

namespace UnitTests.Stores.Default;

public class DefaultPersistedGrantStoreTests
{
    private InMemoryPersistedGrantStore _store = new InMemoryPersistedGrantStore();
    private IAuthorizationCodeStore _codes;
    private IRefreshTokenStore _refreshTokens;
    private IReferenceTokenStore _referenceTokens;
    private IUserConsentStore _userConsent;
    private StubHandleGenerationService _stubHandleGenerationService = new StubHandleGenerationService();

    private ClaimsPrincipal _user = new IdentityServerUser("123").CreatePrincipal();
    private readonly CT _ct = TestContext.Current.CancellationToken;

    public DefaultPersistedGrantStoreTests()
    {
        _codes = new DefaultAuthorizationCodeStore(_store,
            new PersistentGrantSerializer(),
            _stubHandleGenerationService,
            TestLogger.Create<DefaultAuthorizationCodeStore>());
        _refreshTokens = new DefaultRefreshTokenStore(_store,
            new PersistentGrantSerializer(),
            _stubHandleGenerationService,
            TestLogger.Create<DefaultRefreshTokenStore>());
        _referenceTokens = new DefaultReferenceTokenStore(_store,
            new PersistentGrantSerializer(),
            _stubHandleGenerationService,
            TestLogger.Create<DefaultReferenceTokenStore>());
        _userConsent = new DefaultUserConsentStore(_store,
            new PersistentGrantSerializer(),
            _stubHandleGenerationService,
            TestLogger.Create<DefaultUserConsentStore>());
    }

    [Fact]
    public async Task StoreAuthorizationCodeAsync_should_persist_grant()
    {
        var code1 = new AuthorizationCode()
        {
            ClientId = "test",
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Subject = _user,
            CodeChallenge = "challenge",
            RedirectUri = "http://client/cb",
            Nonce = "nonce",
            RequestedScopes = new string[] { "scope1", "scope2" }
        };

        var handle = await _codes.StoreAuthorizationCodeAsync(code1, _ct);
        var code2 = await _codes.GetAuthorizationCodeAsync(handle, _ct);

        code1.ClientId.ShouldBe(code2.ClientId);
        code1.CreationTime.ShouldBe(code2.CreationTime);
        code1.Lifetime.ShouldBe(code2.Lifetime);
        code1.Subject.GetSubjectId().ShouldBe(code2.Subject.GetSubjectId());
        code1.CodeChallenge.ShouldBe(code2.CodeChallenge);
        code1.RedirectUri.ShouldBe(code2.RedirectUri);
        code1.Nonce.ShouldBe(code2.Nonce);
        code1.RequestedScopes.ShouldBe(code2.RequestedScopes);
    }

    [Fact]
    public async Task RemoveAuthorizationCodeAsync_should_remove_grant()
    {
        var code1 = new AuthorizationCode()
        {
            ClientId = "test",
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Subject = _user,
            CodeChallenge = "challenge",
            RedirectUri = "http://client/cb",
            Nonce = "nonce",
            RequestedScopes = new string[] { "scope1", "scope2" }
        };

        var handle = await _codes.StoreAuthorizationCodeAsync(code1, _ct);
        await _codes.RemoveAuthorizationCodeAsync(handle, _ct);
        var code2 = await _codes.GetAuthorizationCodeAsync(handle, _ct);
        code2.ShouldBeNull();
    }

    [Fact]
    public async Task refresh_token_in_pre_version_5_format_should_deserialize()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var token1 = new RefreshToken()
        {
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            SessionId = "sessionid",
            AccessToken = new Token
            {
                ClientId = "client",
                Audiences = { "aud" },
                CreationTime = DateTime.UtcNow,
                Type = "type",
                Description = "desc",
                Claims = new List<Claim>
                {
                    new Claim("sub", "123"),
                    new Claim("sid", "sessionid"),
                    new Claim("scope", "s1"),
                    new Claim("scope", "s2"),
                },
            },
            Version = 4
        };

        var handle = await _refreshTokens.StoreRefreshTokenAsync(token1);
        var token2 = await _refreshTokens.GetRefreshTokenAsync(handle);

        token2.Version.ShouldBe(5);

        token2.ClientId.ShouldBe("client");
        token2.Subject.GetSubjectId().ShouldBe("123");
        token2.SubjectId.ShouldBe("123");
        token2.Description.ShouldBe("desc");
        token2.SessionId.ShouldBe("sessionid");
        token2.AuthorizedScopes.ShouldBe(new[] { "s1", "s2" });
        token2.AccessToken.ShouldBeNull();

#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_should_persist_grant()
    {
        var now = DateTime.UtcNow;
        var token1 = new RefreshToken()
        {
            ClientId = "client",
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            AuthorizedScopes = new[] { "foo" },
            CreationTime = now,
            Lifetime = 10,
        };
        token1.SetAccessToken(new Token
        {
            ClientId = "client",
            Audiences = { "aud" },
            CreationTime = now,
            Type = "type",
            Claims = new List<Claim>
            {
                new Claim("sub", "123"),
                new Claim("scope", "foo")
            }
        });

        var handle = await _refreshTokens.StoreRefreshTokenAsync(token1);
        var token2 = await _refreshTokens.GetRefreshTokenAsync(handle);

        token1.ClientId.ShouldBe(token2.ClientId);
        token1.CreationTime.ShouldBe(token2.CreationTime);
        token1.Lifetime.ShouldBe(token2.Lifetime);
        token1.Subject.GetSubjectId().ShouldBe(token2.Subject.GetSubjectId());
        token1.Version.ShouldBe(token2.Version);
        var at = token2.GetAccessToken();
        at.Audiences.Count.ShouldBe(1);
        at.Audiences.First().ShouldBe("aud");
        at.ClientId.ShouldBe("client");
        at.CreationTime.ShouldBe(now);
        at.Type.ShouldBe("type");
    }

    [Fact]
    public async Task RemoveRefreshTokenAsync_should_remove_grant()
    {
        var token1 = new RefreshToken()
        {
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        };


        var handle = await _refreshTokens.StoreRefreshTokenAsync(token1);
        await _refreshTokens.RemoveRefreshTokenAsync(handle);
        var token2 = await _refreshTokens.GetRefreshTokenAsync(handle);
        token2.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveRefreshTokenAsync_by_sub_and_client_should_remove_grant()
    {
        var token1 = new RefreshToken()
        {
            ClientId = "client",
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            AuthorizedScopes = new[] { "foo" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        };

        var handle1 = await _refreshTokens.StoreRefreshTokenAsync(token1);
        var handle2 = await _refreshTokens.StoreRefreshTokenAsync(token1);
        await _refreshTokens.RemoveRefreshTokensAsync("123", "client");

        var token2 = await _refreshTokens.GetRefreshTokenAsync(handle1);
        token2.ShouldBeNull();
        token2 = await _refreshTokens.GetRefreshTokenAsync(handle2);
        token2.ShouldBeNull();
    }

    [Fact]
    public async Task StoreReferenceTokenAsync_should_persist_grant()
    {
        var token1 = new Token()
        {
            ClientId = "client",
            Audiences = { "aud" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Type = "type",
            Claims = new List<Claim>
            {
                new Claim("sub", "123"),
                new Claim("scope", "foo")
            },
            Version = 1
        };

        var handle = await _referenceTokens.StoreReferenceTokenAsync(token1);
        var token2 = await _referenceTokens.GetReferenceTokenAsync(handle);

        token1.ClientId.ShouldBe(token2.ClientId);
        token1.Audiences.Count.ShouldBe(1);
        token1.Audiences.First().ShouldBe("aud");
        token1.CreationTime.ShouldBe(token2.CreationTime);
        token1.Type.ShouldBe(token2.Type);
        token1.Lifetime.ShouldBe(token2.Lifetime);
        token1.Version.ShouldBe(token2.Version);
    }

    [Fact]
    public async Task RemoveReferenceTokenAsync_should_remove_grant()
    {
        var token1 = new Token()
        {
            ClientId = "client",
            Audiences = { "aud" },
            CreationTime = DateTime.UtcNow,
            Type = "type",
            Claims = new List<Claim>
            {
                new Claim("sub", "123"),
                new Claim("scope", "foo")
            },
            Version = 1
        };

        var handle = await _referenceTokens.StoreReferenceTokenAsync(token1);
        await _referenceTokens.RemoveReferenceTokenAsync(handle);
        var token2 = await _referenceTokens.GetReferenceTokenAsync(handle);
        token2.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveReferenceTokenAsync_by_sub_and_client_should_remove_grant()
    {
        var token1 = new Token()
        {
            ClientId = "client",
            Audiences = { "aud" },
            CreationTime = DateTime.UtcNow,
            Type = "type",
            Claims = new List<Claim>
            {
                new Claim("sub", "123"),
                new Claim("scope", "foo")
            },
            Version = 1
        };

        var handle1 = await _referenceTokens.StoreReferenceTokenAsync(token1);
        var handle2 = await _referenceTokens.StoreReferenceTokenAsync(token1);
        await _referenceTokens.RemoveReferenceTokensAsync("123", "client");

        var token2 = await _referenceTokens.GetReferenceTokenAsync(handle1);
        token2.ShouldBeNull();
        token2 = await _referenceTokens.GetReferenceTokenAsync(handle2);
        token2.ShouldBeNull();
    }

    [Fact]
    public async Task StoreUserConsentAsync_should_persist_grant()
    {
        var consent1 = new Consent()
        {
            CreationTime = DateTime.UtcNow,
            ClientId = "client",
            SubjectId = "123",
            Scopes = new string[] { "foo", "bar" }
        };

        await _userConsent.StoreUserConsentAsync(consent1);
        var consent2 = await _userConsent.GetUserConsentAsync("123", "client");

        consent2.ClientId.ShouldBe(consent1.ClientId);
        consent2.SubjectId.ShouldBe(consent1.SubjectId);
        consent2.Scopes.ShouldBe(["bar", "foo"], true);
    }

    [Fact]
    public async Task RemoveUserConsentAsync_should_remove_grant()
    {
        var consent1 = new Consent()
        {
            CreationTime = DateTime.UtcNow,
            ClientId = "client",
            SubjectId = "123",
            Scopes = new string[] { "foo", "bar" }
        };

        await _userConsent.StoreUserConsentAsync(consent1);
        await _userConsent.RemoveUserConsentAsync("123", "client");
        var consent2 = await _userConsent.GetUserConsentAsync("123", "client");
        consent2.ShouldBeNull();
    }

    [Fact]
    public async Task same_key_for_different_grant_types_should_not_interfere_with_each_other()
    {
        _stubHandleGenerationService.Handle = "key";

        await _referenceTokens.StoreReferenceTokenAsync(new Token()
        {
            ClientId = "client1",
            Audiences = { "aud" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Type = "type",
            Claims = new List<Claim>
            {
                new Claim("sub", "123"),
                new Claim("scope", "bar1"),
                new Claim("scope", "bar2")
            }
        });

        await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
        {
            ClientId = "client1",
            Subject = _user,
            CreationTime = DateTime.UtcNow,
            Lifetime = 20,
        });

        await _codes.StoreAuthorizationCodeAsync(new AuthorizationCode()
        {
            ClientId = "client1",
            CreationTime = DateTime.UtcNow,
            Lifetime = 30,
            Subject = _user,
            CodeChallenge = "challenge",
            RedirectUri = "http://client/cb",
            Nonce = "nonce",
            RequestedScopes = new string[] { "quux1", "quux2" }
        }, _ct);

        // the -1 is needed because internally we append a version/suffix the handle for encoding
        (await _codes.GetAuthorizationCodeAsync("key-1", _ct)).Lifetime.ShouldBe(30);
        (await _refreshTokens.GetRefreshTokenAsync("key-1")).Lifetime.ShouldBe(20);
        (await _referenceTokens.GetReferenceTokenAsync("key-1")).Lifetime.ShouldBe(10);
    }
}
