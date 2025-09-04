// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Test;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Client = Duende.IdentityServer.Models.Client;

namespace Duende.IdentityServer.IntegrationTests.EntityFramework;

public class EntityFrameworkBasedLogoutTests
{
    private readonly IdentityServerPipeline _mockPipeline = new();

    private static readonly ICollection<Client> _clients =
    [
        new()
        {
            ClientId = "client_one",
            ClientName = "Client One",
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequireConsent = false,
            RequirePkce = false,
            AllowedScopes = { "openid", "api" },
            AllowOfflineAccess = true,
            CoordinateLifetimeWithUserSession = true,
            BackChannelLogoutUri = "https://client_one/logout",
            RedirectUris = ["https://client_one/redirect"]
        },
        new()
        {
            ClientId = "client_two",
            ClientName = "Client Two",
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequireConsent = false,
            RequirePkce = false,
            AllowedScopes = { "openid", "api" },
            AllowOfflineAccess = true,
            CoordinateLifetimeWithUserSession = true,
            BackChannelLogoutUri = "https://client_two/logout",
            RedirectUris = ["https://client_two/redirect"]
        }
    ];

    public EntityFrameworkBasedLogoutTests()
    {
        _mockPipeline.Clients.AddRange(_clients);
        _mockPipeline.IdentityScopes.Add(new IdentityResources.OpenId());
        _mockPipeline.ApiScopes.Add(new ApiScope("api"));

        _mockPipeline.Users.Add(new TestUser
        {
            SubjectId = "alice",
            Username = "alice",
        });
    }

    [Fact]
    public async Task LogoutWithMultipleClientsInSession_WhenUsingEntityFrameworkBackedKeyStore_Succeeds()
    {
        //Setup db context with simulated network delay to cause concurrent db access
        var options = DatabaseProviderBuilder.BuildSqlite<PersistedGrantDbContext, OperationalStoreOptions>("NotUsed", new OperationalStoreOptions(),
            TimeSpan.FromMilliseconds(1));
        await using var context = new PersistedGrantDbContext(options);
        await context.Database.EnsureCreatedAsync();

        _mockPipeline.OnPostConfigureServices += services =>
        {
            //Override the default developer signing key store and signing credential store with the EF based implementations to repo bug specific to concurrent access to an EF db context
            services.AddSingleton<ISigningKeyStore>(new SigningKeyStore(context, new NullLogger<SigningKeyStore>(),
                new NoneCancellationTokenProvider()));
            services.Replace(ServiceDescriptor.Singleton<ISigningCredentialStore, AutomaticKeyManagerKeyStore>());
        };
        _mockPipeline.Initialize();
        _mockPipeline.Options.KeyManagement.Enabled = true;

        await _mockPipeline.LoginAsync("alice");

        //Ensure user session is tied to multiple clients so back channel logout will execute against multiple clients
        foreach (var client in _clients)
        {
            var authzResponse = await _mockPipeline.RequestAuthorizationEndpointAsync(client.ClientId, "code", "openid api offline_access", client.RedirectUris.First());
            _ = await _mockPipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = client.ClientId,
                Code = authzResponse.Code,
                RedirectUri = client.RedirectUris.First()
            });
        }

        //Clear cache to simulate needing to load from db when creating logout notifications to send
        var signingKeyStoreCache = _mockPipeline.Resolve<ISigningKeyStoreCache>();
        await signingKeyStoreCache.StoreKeysAsync([], TimeSpan.Zero);

        await _mockPipeline.LogoutAsync();

        _mockPipeline.ErrorWasCalled.ShouldBeFalse();
    }
}
