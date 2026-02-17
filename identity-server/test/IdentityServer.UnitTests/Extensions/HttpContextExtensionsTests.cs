// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Common;

namespace UnitTests.Extensions;

public class HttpContextExtensionsTests
{
    [Fact]
    public async Task GetIdentityServerSignoutFrameCallbackUrlAsync_without_logout_message_returns_null_if_no_clients_have_front_channel_logout_uri()
    {
        var clientWithoutFrontChannelLogoutUrl = new Client
        {
            ClientId = "client_without_frontchannel_logout_url",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireClientSecret = false,
            AllowedScopes = { "api1" }
        };
        var context = CreateContextWithUserSession("Test", clientWithoutFrontChannelLogoutUrl);

        var result = await context.GetIdentityServerSignoutFrameCallbackUrlAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetIdentityServerSignoutFrameCallbackUrlAsync_without_logout_message_returns_url_if_single_client_has_front_channel_logout_uri()
    {
        var clientWithFrontChannelLogoutUrl = new Client
        {
            ClientId = "client_with_front_channel_logout_url",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireClientSecret = false,
            AllowedScopes = { "api1" },
            FrontChannelLogoutUri = "http://foo"
        };
        var clientWithoutFrontChannelLogoutUrl = new Client
        {
            ClientId = "client_without_front_channel_logout_url",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireClientSecret = false,
            AllowedScopes = { "api1" }
        };
        var context = CreateContextWithUserSession("Test", clientWithoutFrontChannelLogoutUrl, clientWithFrontChannelLogoutUrl);

        var result = await context.GetIdentityServerSignoutFrameCallbackUrlAsync();

        result.ShouldStartWith("/connect/endsession/callback?endSessionId=");
    }

    [Fact]
    public async Task GetIdentityServerSignoutFrameCallbackUrlAsync_without_logout_message_returns_url_if_multiple_clients_have_front_channel_logout_uri()
    {
        var firstClientWithFrontChannelLogoutUrl = new Client
        {
            ClientId = "first_client_with_front_channel_logout_url",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireClientSecret = false,
            AllowedScopes = { "api1" },
            FrontChannelLogoutUri = "http://foo"
        };
        var secondClientWithFrontChannelLogoutUrl = new Client
        {
            ClientId = "second_client_with_front_channel_logout_url",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireClientSecret = false,
            AllowedScopes = { "api1" },
            FrontChannelLogoutUri = "http://bar"
        };
        var clientWithoutFrontChannelLogoutUrl = new Client
        {
            ClientId = "client_without_front_channel_logout_url",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireClientSecret = false,
            AllowedScopes = { "api1" }
        };
        var context = CreateContextWithUserSession("Test", firstClientWithFrontChannelLogoutUrl,
            secondClientWithFrontChannelLogoutUrl, clientWithoutFrontChannelLogoutUrl);

        var result = await context.GetIdentityServerSignoutFrameCallbackUrlAsync();

        result.ShouldStartWith("/connect/endsession/callback?endSessionId=");
    }

    [Fact]
    public async Task GetIdentityServerSignoutFrameCallbackUrlAsync_with_logout_message_returns_url_if_logout_message_has_client_with_front_channel_logout_uri()
    {
        var clientWithFrontChannelLogoutUrl = new Client
        {
            ClientId = "client_with_front_channel_logout_url",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireClientSecret = false,
            AllowedScopes = { "api1" },
            FrontChannelLogoutUri = "http://foo"
        };
        var clientWithoutFrontChannelLogoutUrl = new Client
        {
            ClientId = "client_without_front_channel_logout_url",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireClientSecret = false,
            AllowedScopes = { "api1" }
        };
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "Test",
            SessionId = "session-id",
            ClientIds = [clientWithFrontChannelLogoutUrl.ClientId]
        };
        var context = CreateContextWithUserSession("Test", clientWithoutFrontChannelLogoutUrl, clientWithFrontChannelLogoutUrl);

        var result = await context.GetIdentityServerSignoutFrameCallbackUrlAsync(logoutMessage);

        result.ShouldStartWith("/connect/endsession/callback?endSessionId=");
    }

    [Fact]
    public async Task GetIdentityServerSignoutFrameCallbackUrlAsync_with_logout_message_returns_null_if_logout_message_has_no_client_with_front_channel_logout_uri()
    {
        var clientWithoutFrontChannelLogoutUrl = new Client
        {
            ClientId = "client_without_front_channel_logout_url",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireClientSecret = false,
            AllowedScopes = { "api1" }
        };
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "Test",
            SessionId = "session-id",
            ClientIds = [clientWithoutFrontChannelLogoutUrl.ClientId]
        };
        var context = CreateContextWithUserSession("Test", clientWithoutFrontChannelLogoutUrl);

        var result = await context.GetIdentityServerSignoutFrameCallbackUrlAsync(logoutMessage);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetIdentityServerSignoutFrameCallbackUrlAsync_with_logout_message_returns_url_if_logout_message_has_no_clients_but_user_session_has_client_with_front_channel_logout_url()
    {
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "Test",
            SessionId = "session-id",
            ClientIds = []
        };
        var clientWithFrontChannelLogoutUrl = new Client
        {
            ClientId = "client_with_front_channel_logout_url",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireClientSecret = false,
            AllowedScopes = { "api1" },
            FrontChannelLogoutUri = "http://foo"
        };
        var context = CreateContextWithUserSession("Test", clientWithFrontChannelLogoutUrl);

        var result = await context.GetIdentityServerSignoutFrameCallbackUrlAsync(logoutMessage);

        result.ShouldStartWith("/connect/endsession/callback?endSessionId=");
    }

    [Fact]
    public async Task GetIdentityServerSignoutFrameCallbackUrlAsync_with_logout_message_returns_null_if_logout_message_has_no_clients_and_user_session_has_no_clients()
    {
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "Test",
            SessionId = "session-id",
            ClientIds = []
        };
        var context = CreateContextWithUserSession("Test");

        var result = await context.GetIdentityServerSignoutFrameCallbackUrlAsync(logoutMessage);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task
        GetIdentityServerSignoutFrameCallbackUrlAsync_with_logout_message_returns_null_when_logout_message_has_no_clients_and_user_session_has_no_subject_id()
    {
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "Test",
            SessionId = "session-id",
            ClientIds = []
        };
        var context = CreateContextWithUserSession(null);

        var result = await context.GetIdentityServerSignoutFrameCallbackUrlAsync(logoutMessage);

        result.ShouldBeNull();
    }

    private DefaultHttpContext CreateContextWithUserSession(string? subjectId, params Client[] clients)
    {
        var userSession = new MockUserSession
        {
            Clients = clients.Select(client => client.ClientId).ToList(),
        };

        if (subjectId != null)
        {
            userSession.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, subjectId)]));
        }

        var clientStore = new InMemoryClientStore(clients);
        var services = new ServiceCollection();
        services.AddSingleton<IUserSession>(userSession);
        services.AddSingleton<IClientStore>(clientStore);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<IMessageStore<LogoutNotificationContext>, MockMessageStore<LogoutNotificationContext>>();
        services.AddSingleton<IServerUrls>(new MockServerUrls());

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
    }
}
