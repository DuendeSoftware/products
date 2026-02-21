// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class DefaultPersistedGrantServiceTests
{
    private DefaultPersistedGrantService _subject;
    private InMemoryPersistedGrantStore _store = new InMemoryPersistedGrantStore();
    private IAuthorizationCodeStore _codes;
    private IRefreshTokenStore _refreshTokens;
    private IReferenceTokenStore _referenceTokens;
    private IUserConsentStore _userConsent;

    private ClaimsPrincipal _user = new IdentityServerUser("123").CreatePrincipal();
    private readonly CT _ct = TestContext.Current.CancellationToken;

    public DefaultPersistedGrantServiceTests()
    {
        _subject = new DefaultPersistedGrantService(
            _store,
            new PersistentGrantSerializer(),
            TestLogger.Create<DefaultPersistedGrantService>());
        _codes = new DefaultAuthorizationCodeStore(_store,
            new PersistentGrantSerializer(),
            new DefaultHandleGenerationService(),
            TestLogger.Create<DefaultAuthorizationCodeStore>());
        _refreshTokens = new DefaultRefreshTokenStore(_store,
            new PersistentGrantSerializer(),
            new DefaultHandleGenerationService(),
            TestLogger.Create<DefaultRefreshTokenStore>());
        _referenceTokens = new DefaultReferenceTokenStore(_store,
            new PersistentGrantSerializer(),
            new DefaultHandleGenerationService(),
            TestLogger.Create<DefaultReferenceTokenStore>());
        _userConsent = new DefaultUserConsentStore(_store,
            new PersistentGrantSerializer(),
            new DefaultHandleGenerationService(),
            TestLogger.Create<DefaultUserConsentStore>());
    }

    [Fact]
    public async Task GetAllGrantsAsync_should_return_all_grants()
    {
        await _userConsent.StoreUserConsentAsync(new Consent()
        {
            CreationTime = DateTime.UtcNow,
            ClientId = "client1",
            SubjectId = "123",
            Scopes = new string[] { "foo1", "foo2" }
        }, _ct);
        await _userConsent.StoreUserConsentAsync(new Consent()
        {
            CreationTime = DateTime.UtcNow,
            ClientId = "client2",
            SubjectId = "123",
            Scopes = new string[] { "foo3" }
        }, _ct);
        await _userConsent.StoreUserConsentAsync(new Consent()
        {
            CreationTime = DateTime.UtcNow,
            ClientId = "client1",
            SubjectId = "456",
            Scopes = new string[] { "foo3" }
        }, _ct);

        var handle1 = await _referenceTokens.StoreReferenceTokenAsync(new Token()
        {
            ClientId = "client1",
            Audiences = { "aud" },
            CreationTime = DateTime.UtcNow,
            Type = "type",
            Claims = new List<Claim>
            {
                new Claim("sub", "123"),
                new Claim("scope", "bar1"),
                new Claim("scope", "bar2")
            }
        }, _ct);

        var handle2 = await _referenceTokens.StoreReferenceTokenAsync(new Token()
        {
            ClientId = "client2",
            Audiences = { "aud" },
            CreationTime = DateTime.UtcNow,
            Type = "type",
            Claims = new List<Claim>
            {
                new Claim("sub", "123"),
                new Claim("scope", "bar3")
            }
        }, _ct);

        var handle3 = await _referenceTokens.StoreReferenceTokenAsync(new Token()
        {
            ClientId = "client1",
            Audiences = { "aud" },
            CreationTime = DateTime.UtcNow,
            Type = "type",
            Claims = new List<Claim>
            {
                new Claim("sub", "456"),
                new Claim("scope", "bar3")
            }
        }, _ct);

        var handle4 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
        {
            ClientId = "client1",
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            AuthorizedScopes = new[] { "baz1", "baz2" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        }, _ct);
        var handle5 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
        {
            ClientId = "client1",
            Subject = new IdentityServerUser("456").CreatePrincipal(),
            AuthorizedScopes = new[] { "baz3" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        }, _ct);
        var handle6 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
        {
            ClientId = "client2",
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            AuthorizedScopes = new[] { "baz3" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        }, _ct);

        var handle7 = await _codes.StoreAuthorizationCodeAsync(new AuthorizationCode()
        {
            ClientId = "client1",
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Subject = _user,
            CodeChallenge = "challenge",
            RedirectUri = "http://client/cb",
            Nonce = "nonce",
            RequestedScopes = new string[] { "quux1", "quux2" }
        }, _ct);

        var handle8 = await _codes.StoreAuthorizationCodeAsync(new AuthorizationCode()
        {
            ClientId = "client2",
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Subject = _user,
            CodeChallenge = "challenge",
            RedirectUri = "http://client/cb",
            Nonce = "nonce",
            RequestedScopes = new string[] { "quux3" }
        }, _ct);

        var handle9 = await _codes.StoreAuthorizationCodeAsync(new AuthorizationCode()
        {
            ClientId = "client1",
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Subject = new IdentityServerUser("456").CreatePrincipal(),
            CodeChallenge = "challenge",
            RedirectUri = "http://client/cb",
            Nonce = "nonce",
            RequestedScopes = new string[] { "quux3" }
        }, _ct);

        var grants = await _subject.GetAllGrantsAsync("123", _ct);

        grants.Count().ShouldBe(2);
        var grant1 = grants.First(x => x.ClientId == "client1");
        grant1.SubjectId.ShouldBe("123");
        grant1.ClientId.ShouldBe("client1");
        grant1.Scopes.ShouldBe(["foo1", "foo2", "bar1", "bar2", "baz1", "baz2", "quux1", "quux2"], true);

        var grant2 = grants.First(x => x.ClientId == "client2");
        grant2.SubjectId.ShouldBe("123");
        grant2.ClientId.ShouldBe("client2");
        grant2.Scopes.ShouldBe(["foo3", "bar3", "baz3", "quux3"], true);
    }

    [Fact]
    public async Task RemoveAllGrantsAsync_should_remove_all_grants()
    {
        await _userConsent.StoreUserConsentAsync(new Consent()
        {
            ClientId = "client1",
            SubjectId = "123",
            Scopes = new string[] { "foo1", "foo2" }
        }, _ct);
        await _userConsent.StoreUserConsentAsync(new Consent()
        {
            ClientId = "client2",
            SubjectId = "123",
            Scopes = new string[] { "foo3" }
        }, _ct);
        await _userConsent.StoreUserConsentAsync(new Consent()
        {
            ClientId = "client1",
            SubjectId = "456",
            Scopes = new string[] { "foo3" }
        }, _ct);

        var handle1 = await _referenceTokens.StoreReferenceTokenAsync(new Token()
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
        }, _ct);

        var handle2 = await _referenceTokens.StoreReferenceTokenAsync(new Token()
        {
            ClientId = "client2",
            Audiences = { "aud" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Type = "type",
            Claims = new List<Claim>
            {
                new Claim("sub", "123"),
                new Claim("scope", "bar3")
            }
        }, _ct);

        var handle3 = await _referenceTokens.StoreReferenceTokenAsync(new Token()
        {
            ClientId = "client1",
            Audiences = { "aud" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Type = "type",
            Claims = new List<Claim>
            {
                new Claim("sub", "456"),
                new Claim("scope", "bar3")
            }
        }, _ct);

        var handle4 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
        {
            ClientId = "client1",
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            AuthorizedScopes = new[] { "baz1", "baz2" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        }, _ct);
        var handle5 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
        {
            ClientId = "client1",
            Subject = new IdentityServerUser("456").CreatePrincipal(),
            AuthorizedScopes = new[] { "baz3" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        }, _ct);
        var handle6 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
        {
            ClientId = "client2",
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            AuthorizedScopes = new[] { "baz3" },
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
        }, _ct);

        var handle7 = await _codes.StoreAuthorizationCodeAsync(new AuthorizationCode()
        {
            ClientId = "client1",
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Subject = _user,
            CodeChallenge = "challenge",
            RedirectUri = "http://client/cb",
            Nonce = "nonce",
            RequestedScopes = new string[] { "quux1", "quux2" }
        }, _ct);

        var handle8 = await _codes.StoreAuthorizationCodeAsync(new AuthorizationCode()
        {
            ClientId = "client2",
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Subject = _user,
            CodeChallenge = "challenge",
            RedirectUri = "http://client/cb",
            Nonce = "nonce",
            RequestedScopes = new string[] { "quux3" }
        }, _ct);

        var handle9 = await _codes.StoreAuthorizationCodeAsync(new AuthorizationCode()
        {
            ClientId = "client1",
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Subject = new IdentityServerUser("456").CreatePrincipal(),
            CodeChallenge = "challenge",
            RedirectUri = "http://client/cb",
            Nonce = "nonce",
            RequestedScopes = new string[] { "quux3" }
        }, _ct);

        await _subject.RemoveAllGrantsAsync("123", _ct, "client1");

        (await _referenceTokens.GetReferenceTokenAsync(handle1, _ct)).ShouldBeNull();
        (await _referenceTokens.GetReferenceTokenAsync(handle2, _ct)).ShouldNotBeNull();
        (await _referenceTokens.GetReferenceTokenAsync(handle3, _ct)).ShouldNotBeNull();
        (await _refreshTokens.GetRefreshTokenAsync(handle4, _ct)).ShouldBeNull();
        (await _refreshTokens.GetRefreshTokenAsync(handle5, _ct)).ShouldNotBeNull();
        (await _refreshTokens.GetRefreshTokenAsync(handle6, _ct)).ShouldNotBeNull();
        (await _codes.GetAuthorizationCodeAsync(handle7, _ct)).ShouldBeNull();
        (await _codes.GetAuthorizationCodeAsync(handle8, _ct)).ShouldNotBeNull();
        (await _codes.GetAuthorizationCodeAsync(handle9, _ct)).ShouldNotBeNull();
    }
    [Fact]
    public async Task RemoveAllGrantsAsync_should_filter_on_session_id()
    {
        {
            var handle1 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client1",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session1",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            var handle2 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client2",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session1",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            var handle3 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client3",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session3",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);

            await _subject.RemoveAllGrantsAsync("123", _ct);

            (await _refreshTokens.GetRefreshTokenAsync(handle1, _ct)).ShouldBeNull();
            (await _refreshTokens.GetRefreshTokenAsync(handle2, _ct)).ShouldBeNull();
            (await _refreshTokens.GetRefreshTokenAsync(handle3, _ct)).ShouldBeNull();
            await _refreshTokens.RemoveRefreshTokenAsync(handle1, _ct);
            await _refreshTokens.RemoveRefreshTokenAsync(handle2, _ct);
            await _refreshTokens.RemoveRefreshTokenAsync(handle3, _ct);
        }
        {
            var handle1 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client1",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session1",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            var handle2 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client2",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session1",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            var handle3 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client3",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session3",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);

            await _subject.RemoveAllGrantsAsync("123", _ct, "client1");

            (await _refreshTokens.GetRefreshTokenAsync(handle1, _ct)).ShouldBeNull();
            (await _refreshTokens.GetRefreshTokenAsync(handle2, _ct)).ShouldNotBeNull();
            (await _refreshTokens.GetRefreshTokenAsync(handle3, _ct)).ShouldNotBeNull();
            await _refreshTokens.RemoveRefreshTokenAsync(handle1, _ct);
            await _refreshTokens.RemoveRefreshTokenAsync(handle2, _ct);
            await _refreshTokens.RemoveRefreshTokenAsync(handle3, _ct);
        }
        {
            var handle1 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client1",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session1",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            var handle2 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client2",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session1",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            var handle3 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client3",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session1",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            var handle4 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client1",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session2",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            await _subject.RemoveAllGrantsAsync("123", _ct, "client1", "session1");

            (await _refreshTokens.GetRefreshTokenAsync(handle1, _ct)).ShouldBeNull();
            (await _refreshTokens.GetRefreshTokenAsync(handle2, _ct)).ShouldNotBeNull();
            (await _refreshTokens.GetRefreshTokenAsync(handle3, _ct)).ShouldNotBeNull();
            (await _refreshTokens.GetRefreshTokenAsync(handle4, _ct)).ShouldNotBeNull();
            await _refreshTokens.RemoveRefreshTokenAsync(handle1, _ct);
            await _refreshTokens.RemoveRefreshTokenAsync(handle2, _ct);
            await _refreshTokens.RemoveRefreshTokenAsync(handle3, _ct);
            await _refreshTokens.RemoveRefreshTokenAsync(handle4, _ct);
        }
        {
            var handle1 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client1",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session1",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            var handle2 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client2",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session1",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            var handle3 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client3",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session1",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            var handle4 = await _refreshTokens.StoreRefreshTokenAsync(new RefreshToken()
            {
                ClientId = "client1",
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                SessionId = "session2",
                AuthorizedScopes = new[] { "baz" },
                CreationTime = DateTime.UtcNow,
                Lifetime = 10,
            }, _ct);
            await _subject.RemoveAllGrantsAsync("123", _ct, sessionId: "session1");

            (await _refreshTokens.GetRefreshTokenAsync(handle1, _ct)).ShouldBeNull();
            (await _refreshTokens.GetRefreshTokenAsync(handle2, _ct)).ShouldBeNull();
            (await _refreshTokens.GetRefreshTokenAsync(handle3, _ct)).ShouldBeNull();
            (await _refreshTokens.GetRefreshTokenAsync(handle4, _ct)).ShouldNotBeNull();
            await _refreshTokens.RemoveRefreshTokenAsync(handle1, _ct);
            await _refreshTokens.RemoveRefreshTokenAsync(handle2, _ct);
            await _refreshTokens.RemoveRefreshTokenAsync(handle3, _ct);
            await _refreshTokens.RemoveRefreshTokenAsync(handle4, _ct);
        }
    }

    [Fact]
    public async Task GetAllGrantsAsync_should_aggregate_correctly()
    {
        await _userConsent.StoreUserConsentAsync(new Consent()
        {
            ClientId = "client1",
            SubjectId = "123",
            Scopes = new string[] { "foo1", "foo2" }
        }, _ct);

        var grants = await _subject.GetAllGrantsAsync("123", _ct);

        grants.Count().ShouldBe(1);
        grants.First().Scopes.ShouldBe(["foo1", "foo2"]);

        var handle9 = await _codes.StoreAuthorizationCodeAsync(new AuthorizationCode()
        {
            ClientId = "client1",
            CreationTime = DateTime.UtcNow,
            Lifetime = 10,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            CodeChallenge = "challenge",
            RedirectUri = "http://client/cb",
            Nonce = "nonce",
            RequestedScopes = new string[] { "quux3" }
        }, _ct);

        grants = await _subject.GetAllGrantsAsync("123", _ct);

        grants.Count().ShouldBe(1);
        grants.First().Scopes.ShouldBe(["foo1", "foo2", "quux3"]);
    }

    [Fact]
    public async Task GetAllGrantsAsync_should_filter_items_with_corrupt_data_from_result()
    {
        var mockStore = new CorruptingPersistedGrantStore(_store)
        {
            ClientIdToCorrupt = "client2"
        };

        _subject = new DefaultPersistedGrantService(
           mockStore,
           new PersistentGrantSerializer(),
           TestLogger.Create<DefaultPersistedGrantService>());

        await _userConsent.StoreUserConsentAsync(new Consent()
        {
            ClientId = "client1",
            SubjectId = "123",
            Scopes = new string[] { "foo1", "foo2" }
        }, _ct);
        await _userConsent.StoreUserConsentAsync(new Consent()
        {
            ClientId = "client2",
            SubjectId = "123",
            Scopes = new string[] { "foo3" }
        }, _ct);

        var grants = await _subject.GetAllGrantsAsync("123", _ct);

        grants.Count().ShouldBe(1);
        grants.First().Scopes.ShouldBe(["foo1", "foo2"]);
    }

    private class CorruptingPersistedGrantStore : IPersistedGrantStore
    {
        public string ClientIdToCorrupt { get; set; }

        private IPersistedGrantStore _inner;

        public CorruptingPersistedGrantStore(IPersistedGrantStore inner) => _inner = inner;

        public async Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter, CT ct)
        {
            var items = await _inner.GetAllAsync(filter, ct);
            if (ClientIdToCorrupt != null)
            {
                var itemsToCorrupt = items.Where(x => x.ClientId == ClientIdToCorrupt);
                foreach (var corruptItem in itemsToCorrupt)
                {
                    corruptItem.Data = "corrupt";
                }
            }
            return items;
        }

        public Task<PersistedGrant> GetAsync(string key, CT ct) => _inner.GetAsync(key, ct);

        public Task RemoveAllAsync(PersistedGrantFilter filter, CT ct) => _inner.RemoveAllAsync(filter, ct);

        public Task RemoveAsync(string key, CT ct) => _inner.RemoveAsync(key, ct);

        public Task StoreAsync(PersistedGrant grant, CT ct) => _inner.StoreAsync(grant, ct);
    }
}
