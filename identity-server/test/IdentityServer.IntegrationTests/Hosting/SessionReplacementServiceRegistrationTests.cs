// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Test;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class SessionReplacementServiceRegistrationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static IdentityServerPipeline CreatePipeline(Action<IServiceCollection> configureServices = null)
    {
        var pipeline = new IdentityServerPipeline();

        pipeline.OnPostConfigureServices += s =>
        {
            s.AddIdentityServerBuilder().AddServerSideSessions();
            configureServices?.Invoke(s);
        };

        pipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
        });
        pipeline.Users.Add(new TestUser
        {
            SubjectId = "alice",
            Username = "alice",
        });

        pipeline.Clients.Add(new Client
        {
            ClientId = "client",
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequireConsent = false,
            RequirePkce = false,
            AllowedScopes = { "openid", "api" },
            AllowOfflineAccess = true,
            CoordinateLifetimeWithUserSession = true,
            RefreshTokenUsage = TokenUsage.ReUse,
            RedirectUris = { "https://client/callback" },
        });
        pipeline.IdentityScopes.Add(new IdentityResources.OpenId());
        pipeline.ApiScopes.Add(new ApiScope("api"));

        return pipeline;
    }

    private static async Task GetRefreshTokenAsync(IdentityServerPipeline pipeline)
    {
        var authzResponse = await pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });
        tokenResponse.IsError.ShouldBeFalse();
        tokenResponse.RefreshToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task custom_session_coordination_service_does_not_disable_replacement_cleanup()
    {
        var pipeline = CreatePipeline(s => s.AddTransient<ISessionCoordinationService, CustomSessionCoordinationService>());
        pipeline.Initialize();

        var grantStore = pipeline.Resolve<IPersistedGrantStore>();

        await pipeline.LoginAsync("bob");
        await GetRefreshTokenAsync(pipeline);

        (await grantStore.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "bob",
            ClientId = "client",
            Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken
        }, _ct)).ShouldNotBeEmpty();

        await pipeline.LoginAsync("alice");

        (await grantStore.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "bob",
            ClientId = "client",
            Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken
        }, _ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task custom_session_replacement_service_replaces_default()
    {
        var recordingService = new RecordingSessionReplacementService();
        var pipeline = CreatePipeline(s => s.AddSingleton<ISessionReplacementService>(recordingService));
        pipeline.Initialize();

        var grantStore = pipeline.Resolve<IPersistedGrantStore>();

        await pipeline.LoginAsync("bob");
        await GetRefreshTokenAsync(pipeline);

        await pipeline.LoginAsync("alice");

        recordingService.Calls.Count.ShouldBe(1);
        recordingService.Calls.Single().PreviousSession.SubjectId.ShouldBe("bob");
        recordingService.Calls.Single().NewSubjectId.ShouldBe("alice");

        (await grantStore.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "bob",
            ClientId = "client",
            Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken
        }, _ct)).ShouldNotBeEmpty();
    }

    [Fact]
    public void default_registration_is_DefaultSessionReplacementService()
    {
        var pipeline = CreatePipeline();
        pipeline.Initialize();

        pipeline.Resolve<ISessionReplacementService>().ShouldBeOfType<DefaultSessionReplacementService>();
    }

    private sealed class CustomSessionCoordinationService : ISessionCoordinationService
    {
        public Task ProcessLogoutAsync(UserSession session, Ct ct) => Task.CompletedTask;

        public Task ProcessExpirationAsync(UserSession session, Ct ct) => Task.CompletedTask;

        public Task<bool> ValidateSessionAsync(SessionValidationRequest request, Ct ct) => Task.FromResult(true);
    }

    private sealed class RecordingSessionReplacementService : ISessionReplacementService
    {
        public List<SessionReplacementContext> Calls { get; } = new();

        public Task ProcessReplacementAsync(SessionReplacementContext context, Ct ct)
        {
            Calls.Add(context);
            return Task.CompletedTask;
        }
    }
}
