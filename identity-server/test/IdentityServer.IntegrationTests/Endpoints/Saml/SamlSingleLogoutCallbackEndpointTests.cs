// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlSingleLogoutCallbackEndpointTests
{
    private const string Category = "SAML single logout callback endpoint";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private SamlFixture Fixture = new();

    private SamlData Data => Fixture.Data;

    private SamlDataBuilder Build => Fixture.Builder;

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_with_post_method_should_return_method_not_allowed()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Act
        var result = await Fixture.Client.PostAsync("/saml/logout_callback", new StringContent(""), _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_with_missing_logout_id_should_return_bad_request()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Act
        var result = await Fixture.Client.GetAsync("/saml/logout_callback", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_with_invalid_logout_id_should_return_bad_request()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Act
        var result = await Fixture.Client.GetAsync("/saml/logout_callback?logoutId=invalid", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_with_valid_logout_id_should_return_success_response()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Store a logout message as if SP-initiated logout occurred
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session456",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_abc123",
            SamlRelayState = null
        };
        var messageStore = Fixture.Get<IMessageStore<LogoutMessage>>();
        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage, DateTime.UtcNow), _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout_callback?logoutId={logoutId}", _ct);

        // Assert
        var samlResponse = await SamlTestHelpers.ExtractSamlLogoutResponseFromPostAsync(result, _ct);
        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_should_include_relay_state_if_present()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session456",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_abc123",
            SamlRelayState = "mystate123"
        };
        var messageStore = Fixture.Get<IMessageStore<LogoutMessage>>();
        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage, DateTime.UtcNow), _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout_callback?logoutId={logoutId}", _ct);

        // Assert
        var response = await SamlTestHelpers.ExtractSamlLogoutResponseFromPostAsync(result, _ct);
        response.RelayState.ShouldBe(logoutMessage.SamlRelayState);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_with_disabled_service_provider_should_return_bad_request()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.Enabled = false;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session456",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_abc123"
        };
        var messageStore = Fixture.Get<IMessageStore<LogoutMessage>>();
        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage, DateTime.UtcNow), _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout_callback?logoutId={logoutId}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
