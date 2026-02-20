// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class DefaultBackchannelAuthenticationInteractionServiceTests
{
    private Client _client;
    private DefaultBackchannelAuthenticationInteractionService _subject;
    private readonly CT _ct = TestContext.Current.CancellationToken;

    private MockBackChannelAuthenticationRequestStore _mockStore = new MockBackChannelAuthenticationRequestStore();
    private InMemoryClientStore _clientStore;
    private List<Client> _clients = new List<Client>();
    private MockUserSession _mockUserSession = new MockUserSession();
    private FakeTimeProvider _mockTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    private MockResourceValidator _mockResourceValidator = new MockResourceValidator();

    public DefaultBackchannelAuthenticationInteractionServiceTests()
    {
        _clients.Add(_client = new Client
        {
            ClientId = "client",
        });

        _clientStore = new InMemoryClientStore(_clients);
        _subject = new DefaultBackchannelAuthenticationInteractionService(
            _mockStore,
            _clientStore,
            _mockUserSession,
            _mockResourceValidator,
            _mockTimeProvider,
            TestLogger.Create<DefaultBackchannelAuthenticationInteractionService>());
    }

    [Fact]
    public async Task GetPendingLoginRequestsForCurrentUserAsync_should_use_current_sub_to_filter_results()
    {
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        var req = new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
        };
        await _mockStore.CreateRequestAsync(req, _ct);
        await _mockStore.CreateRequestAsync(new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("other").CreatePrincipal()
        }, _ct);

        var results = await _subject.GetPendingLoginRequestsForCurrentUserAsync(_ct);
        results.Count().ShouldBe(1);
        results.First().InternalId.ShouldBe(req.InternalId);
    }

    [Fact]
    public async Task GetLoginRequestByInternalIdAsync_should_return_correct_item()
    {
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        var req = new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
        };
        var requestId = await _mockStore.CreateRequestAsync(req, _ct);
        await _mockStore.CreateRequestAsync(new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("other").CreatePrincipal()
        }, _ct);

        var result = await _subject.GetLoginRequestByInternalIdAsync(req.InternalId, _ct);
        result.InternalId.ShouldBe(req.InternalId);
    }

    [Fact]
    public async Task CompleteLoginRequestAsync_for_valid_request_should_mark_login_request_complete()
    {
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        var req = new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            RequestedScopes = new[] { "scope1", "scope2", "scope3" },
        };
        var requestId = await _mockStore.CreateRequestAsync(req, _ct);

        await _subject.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(req.InternalId)
        {
            Description = "desc",
            ScopesValuesConsented = new string[] { "scope1", "scope2" },
            SessionId = "sid",
            Subject = new IdentityServerUser("123")
            {
                DisplayName = "name",
                AuthenticationTime = new DateTime(2000, 02, 03, 8, 15, 00, DateTimeKind.Utc),
                IdentityProvider = "idp",
                AdditionalClaims = { new Claim("foo", "bar") },
                AuthenticationMethods = { "phone", "pin" }
            }.CreatePrincipal()
        }, _ct);

        var item = _mockStore.Items[requestId];
        item.IsComplete.ShouldBeTrue();
        item.Description.ShouldBe("desc");
        item.SessionId.ShouldBe("sid");
        item.AuthorizedScopes.ShouldBe(["scope2", "scope1"], true);

        item.Subject.HasClaim("sub", "123").ShouldBeTrue();
        item.Subject.HasClaim("foo", "bar").ShouldBeTrue();
        item.Subject.HasClaim("amr", "phone").ShouldBeTrue();
        item.Subject.HasClaim("amr", "pin").ShouldBeTrue();
        item.Subject.HasClaim("idp", "idp").ShouldBeTrue();
        item.Subject.HasClaim("auth_time", new DateTimeOffset(new DateTime(2000, 02, 03, 8, 15, 00, DateTimeKind.Utc)).ToUnixTimeSeconds().ToString()).ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteLoginRequestAsync_should_require_request_object()
    {
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        var req = new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            RequestedScopes = new[] { "scope1", "scope2", "scope3" },
        };
        var requestId = await _mockStore.CreateRequestAsync(req, _ct);

        var f = async () => await _subject.CompleteLoginRequestAsync(null, _ct);
        await f.ShouldThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CompleteLoginRequestAsync_should_prevent_scopes_not_requested()
    {
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        var req = new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            RequestedScopes = new[] { "scope1", "scope2", "scope3" },
        };
        var requestId = await _mockStore.CreateRequestAsync(req, _ct);

        var f = async () => await _subject.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(req.InternalId)
        {
            Description = "desc",
            ScopesValuesConsented = new string[] { "scope1", "invalid" },
            SessionId = "sid",
            Subject = new IdentityServerUser("123")
            {
                DisplayName = "name",
                AuthenticationTime = new DateTime(2000, 02, 03, 8, 15, 00, DateTimeKind.Utc),
                IdentityProvider = "idp",
                AdditionalClaims = { new Claim("foo", "bar") },
                AuthenticationMethods = { "phone", "pin" }
            }.CreatePrincipal()
        }, _ct);
        var exception = await f.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("More scopes consented than originally requested.");
    }

    [Fact]
    public async Task CompleteLoginRequestAsync_should_prevent_invalid_subject_id()
    {
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        var req = new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            RequestedScopes = new[] { "scope1", "scope2", "scope3" },
        };
        var requestId = await _mockStore.CreateRequestAsync(req, _ct);

        var f = async () => await _subject.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(req.InternalId)
        {
            Description = "desc",
            ScopesValuesConsented = new string[] { "scope1", "invalid" },
            SessionId = "sid",
            Subject = new IdentityServerUser("invalid")
            {
                DisplayName = "name",
                AuthenticationTime = new DateTime(2000, 02, 03, 8, 15, 00, DateTimeKind.Utc),
                IdentityProvider = "idp",
                AdditionalClaims = { new Claim("foo", "bar") },
                AuthenticationMethods = { "phone", "pin" }
            }.CreatePrincipal()
        }, _ct);
        var exception = await f.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("User's subject id: 'invalid' does not match subject id for backchannel authentication request: '123'.");
    }

    [Fact]
    public async Task CompleteLoginRequestAsync_should_require_a_subject()
    {
        var req = new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            RequestedScopes = new[] { "scope1", "scope2", "scope3" },
        };
        var requestId = await _mockStore.CreateRequestAsync(req, _ct);

        var f = async () => await _subject.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(req.InternalId)
        {
            Description = "desc",
            ScopesValuesConsented = new string[] { "scope1", "invalid" },
            SessionId = "sid",
            //Subject = new IdentityServerUser("invalid")
            //{
            //    DisplayName = "name",
            //    AuthenticationTime = new DateTime(2000, 02, 03, 8, 15, 00, DateTimeKind.Utc),
            //    IdentityProvider = "idp",
            //    AdditionalClaims = { new Claim("foo", "bar") },
            //    AuthenticationMethods = { "phone", "pin" }
            //}.CreatePrincipal()
        }, _ct);
        var exception = await f.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("Invalid subject.");
    }

    [Fact]
    public async Task CompleteLoginRequestAsync_should_require_a_valid_request_id()
    {
        var req = new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            RequestedScopes = new[] { "scope1", "scope2", "scope3" },
        };
        var requestId = await _mockStore.CreateRequestAsync(req, _ct);

        var f = async () => await _subject.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest("invalid")
        {
            Description = "desc",
            ScopesValuesConsented = new string[] { "scope1", "invalid" },
            SessionId = "sid",
            Subject = new IdentityServerUser("123")
            {
                DisplayName = "name",
                AuthenticationTime = new DateTime(2000, 02, 03, 8, 15, 00, DateTimeKind.Utc),
                IdentityProvider = "idp",
                AdditionalClaims = { new Claim("foo", "bar") },
                AuthenticationMethods = { "phone", "pin" }
            }.CreatePrincipal()
        }, _ct);
        var exception = await f.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("Invalid backchannel authentication request id.");
    }

    [Fact]
    public async Task CompleteLoginRequestAsync_should_use_session_if_no_user_passed()
    {
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        var req = new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            RequestedScopes = new[] { "scope1", "scope2", "scope3" },
        };
        var requestId = await _mockStore.CreateRequestAsync(req, _ct);

        _mockUserSession.User = new IdentityServerUser("123")
        {
            DisplayName = "name",
            AuthenticationTime = new DateTime(2000, 02, 03, 8, 15, 00, DateTimeKind.Utc),
            IdentityProvider = "idp",
            AdditionalClaims = { new Claim("foo", "bar") },
            AuthenticationMethods = { "phone", "pin" }
        }.CreatePrincipal();
        _mockUserSession.SessionId = "session id";

        await _subject.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(req.InternalId)
        {
            Description = "desc",
            ScopesValuesConsented = new string[] { "scope1", "scope2" },
            SessionId = "ignored",
            //Subject = 
        }, _ct);

        var item = _mockStore.Items[requestId];
        item.SessionId.ShouldBe("session id");

        item.Subject.HasClaim("sub", "123").ShouldBeTrue();
        item.Subject.HasClaim("foo", "bar").ShouldBeTrue();
        item.Subject.HasClaim("amr", "phone").ShouldBeTrue();
        item.Subject.HasClaim("amr", "pin").ShouldBeTrue();
        item.Subject.HasClaim("idp", "idp").ShouldBeTrue();
        item.Subject.HasClaim("auth_time", new DateTimeOffset(new DateTime(2000, 02, 03, 8, 15, 00, DateTimeKind.Utc)).ToUnixTimeSeconds().ToString()).ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteLoginRequestAsync_should_default_idp_and_authtime()
    {
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        var req = new BackChannelAuthenticationRequest
        {
            ClientId = _client.ClientId,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            RequestedScopes = new[] { "scope1", "scope2", "scope3" },
        };
        var requestId = await _mockStore.CreateRequestAsync(req, _ct);

        await _subject.CompleteLoginRequestAsync(new CompleteBackchannelLoginRequest(req.InternalId)
        {
            Description = "desc",
            ScopesValuesConsented = new string[] { "scope1", "scope2" },
            SessionId = "sid",
            Subject = new IdentityServerUser("123")
            {
                DisplayName = "name",
                //AuthenticationTime = _mockTimeProvider.UtcNow.UtcDateTime,
                //IdentityProvider = "idp",
                AdditionalClaims = { new Claim("foo", "bar") },
                AuthenticationMethods = { "phone", "pin" }
            }.CreatePrincipal()
        }, _ct);

        var item = _mockStore.Items[requestId];
        item.Subject.HasClaim("idp", "local").ShouldBeTrue();
        item.Subject.HasClaim("auth_time", _mockTimeProvider.GetUtcNow().ToUnixTimeSeconds().ToString()).ShouldBeTrue();
    }
}
