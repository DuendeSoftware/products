// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Hosting.FederatedSignOut;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace UnitTests.Hosting.FederatedSignOut;

public sealed class AuthenticationRequestHandlerWrapperTests
{
    private readonly DefaultHttpContext _httpContext;
    private readonly MockInnerHandler _innerHandler;
    private readonly MockUserSession _userSession;
    private readonly MockServerUrls _serverUrls;
    private readonly MockMessageStore<LogoutMessage> _logoutMessageStore;
    private readonly MockMessageStore<LogoutNotificationContext> _notificationMessageStore;
    private readonly MockMessageStore<SamlSpLogoutMessage> _samlSpLogoutMessageStore;
    private readonly MockClientStore _clientStore;
    private readonly AuthenticationRequestHandlerWrapper _sut;

    public AuthenticationRequestHandlerWrapperTests()
    {
        _httpContext = new DefaultHttpContext();
        _innerHandler = new MockInnerHandler();
        _userSession = new MockUserSession();
        _serverUrls = new MockServerUrls { Origin = "https://identity.example.com", BasePath = "" };
        _logoutMessageStore = new MockMessageStore<LogoutMessage>();
        _notificationMessageStore = new MockMessageStore<LogoutNotificationContext>();
        _samlSpLogoutMessageStore = new MockMessageStore<SamlSpLogoutMessage>();
        _clientStore = new MockClientStore();

        var services = new ServiceCollection();
        services.AddSingleton<IUserSession>(_userSession);
        services.AddSingleton<IServerUrls>(_serverUrls);
        services.AddSingleton<IMessageStore<LogoutMessage>>(_logoutMessageStore);
        services.AddSingleton<IMessageStore<LogoutNotificationContext>>(_notificationMessageStore);
        services.AddSingleton<IMessageStore<SamlSpLogoutMessage>>(_samlSpLogoutMessageStore);
        services.AddSingleton<IClientStore>(_clientStore);
        services.AddSingleton<ISamlServiceProviderStore>(new InMemorySamlServiceProviderStore([]));
        services.AddSingleton<IAuthenticationService>(new MockAuthenticationService
        {
            Result = AuthenticateResult.NoResult()
        });
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        _httpContext.RequestServices = services.BuildServiceProvider();

        var accessor = new MockHttpContextAccessor { HttpContext = _httpContext };
        _sut = new AuthenticationRequestHandlerWrapper(_innerHandler, accessor);
    }

    [Fact]
    public async Task HandleRequestAsync_WhenInnerReturnsFalse_ReturnsFalse()
    {
        _innerHandler.Result = false;

        var result = await _sut.HandleRequestAsync();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequestAsync_WhenInnerReturnsTrueButSignOutNotCalled_ReturnsTrue()
    {
        _innerHandler.Result = true;
        // SignOutCalled not set in Items

        var result = await _sut.HandleRequestAsync();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequestAsync_OidcPath_WhenStatusCode200_CallsProcessFederatedSignOut()
    {
        _innerHandler.Result = true;
        SetSignOutCalled();
        _httpContext.Response.StatusCode = 200;

        // Set up a user with a client that has front-channel logout
        _userSession.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user1")], "test"));
        _userSession.SessionId = "sess1";
        _userSession.Clients.Add("client1");
        _clientStore.Clients.Add(new Client { ClientId = "client1", FrontChannelLogoutUri = "https://client1.example.com/signout" });

        var result = await _sut.HandleRequestAsync();

        result.ShouldBeTrue();
        // Should have written an iframe to the response body
        _httpContext.Response.ContentType.ShouldBe("text/html");
    }

    [Fact]
    public async Task HandleRequestAsync_OidcPath_WhenNoDownstreamClients_DoesNotRenderIframe()
    {
        _innerHandler.Result = true;
        SetSignOutCalled();
        _httpContext.Response.StatusCode = 200;

        // No user, so no clients
        _userSession.User = null;

        var result = await _sut.HandleRequestAsync();

        result.ShouldBeTrue();
        // ContentType should not be set to text/html since no iframe rendered
        _httpContext.Response.ContentType.ShouldBeNull();
    }

    [Fact]
    public async Task HandleRequestAsync_SamlPath_WhenNoDownstreamClients_RedirectsToCompletionEndpoint()
    {
        _innerHandler.Result = true;
        SetSignOutCalled();
        SetSamlContext();
        _httpContext.Response.StatusCode = 303;

        // No user → no downstream clients
        _userSession.User = null;

        var result = await _sut.HandleRequestAsync();

        result.ShouldBeTrue();
        // Should redirect to the SP logout completion endpoint
        _httpContext.Response.StatusCode.ShouldBe(302);
        _httpContext.Response.Headers.Location.ToString().ShouldContain("/saml/slo/sp-complete");
        _httpContext.Response.Headers.Location.ToString().ShouldContain("logoutId=");
    }

    [Fact]
    public async Task HandleRequestAsync_SamlPath_WhenDownstreamClientsExist_RendersCombinedPage()
    {
        _innerHandler.Result = true;
        SetSignOutCalled();
        SetSamlContext();
        _httpContext.Response.StatusCode = 303;
        _httpContext.Response.Body = new MemoryStream();

        // Set up user with front-channel client
        _userSession.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user1")], "test"));
        _userSession.SessionId = "sess1";
        _userSession.Clients.Add("client1");
        _clientStore.Clients.Add(new Client { ClientId = "client1", FrontChannelLogoutUri = "https://client1.example.com/signout" });

        var result = await _sut.HandleRequestAsync();

        result.ShouldBeTrue();
        _httpContext.Response.StatusCode.ShouldBe(200);
        _httpContext.Response.ContentType.ShouldBe("text/html; charset=UTF-8");

        // Read rendered body
        _httpContext.Response.Body.Position = 0;
        var body = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
        body.ShouldContain("iframe");
        body.ShouldContain("saml/slo/sp-complete");

        // Should have stored a SamlSpLogoutMessage
        _samlSpLogoutMessageStore.Messages.Count.ShouldBe(1);
    }

    private void SetSignOutCalled() =>
        _httpContext.Items[Constants.EnvironmentKeys.SignOutCalled] = "true";

    private void SetSamlContext() =>
        _httpContext.Items[SamlSpLogoutContext.HttpContextItemsKey] = new SamlSpLogoutContext
        {
            IdpEntityId = "https://idp.example.com",
            LogoutRequestId = "_request123",
            RelayState = "relay",
            ResponseBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect",
            ResponseDestination = "https://idp.example.com/slo"
        };

    private sealed class MockInnerHandler : IAuthenticationRequestHandler
    {
        public bool Result { get; set; }

        public Task<bool> HandleRequestAsync() => Task.FromResult(Result);
        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => Task.CompletedTask;
        public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(AuthenticationProperties properties) => Task.CompletedTask;
        public Task ForbidAsync(AuthenticationProperties properties) => Task.CompletedTask;
    }

    private sealed class MockClientStore : IClientStore
    {
        public List<Client> Clients { get; set; } = [];

        public Task<Client> FindClientByIdAsync(string clientId, Ct _) =>
            Task.FromResult(Clients.FirstOrDefault(c => c.ClientId == clientId));

        public Task<Client> FindEnabledClientByIdAsync(string clientId, Ct _) =>
            Task.FromResult(Clients.FirstOrDefault(c => c.ClientId == clientId));

        public async IAsyncEnumerable<Client> GetAllClientsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] Ct _)
        {
            foreach (var client in Clients)
            {
                yield return client;
            }
            await Task.CompletedTask;
        }
    }
}
