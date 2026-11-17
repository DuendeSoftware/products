// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Text.Json;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Validation;
using Duende.Spaces;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class SpacesInMemoryRefreshTokenTests(WebServerFixture webServerFixture)
{
    private const string ClientId = "shared-client-id";
    private const string Secret = "secret";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private sealed class AcceptAllPasswordValidator : IResourceOwnerPasswordValidator
    {
        public Task ValidateAsync(ResourceOwnerPasswordValidationContext context, Ct ct)
        {
            context.Result = new GrantValidationResult("alice", "password");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task refresh_token_issued_in_one_space_is_rejected_in_another_space_with_the_in_memory_grant_store()
    {
        var dbName = $"msrefresh_{Guid.NewGuid():N}";
        await using var server = new KestrelBasedTestServer(
            $"ms{Guid.NewGuid():N}"[..10],
            webServerFixture,
            new PrefixedTestOutputHelper(TestContext.Current.TestOutputHelper!, nameof(SpacesInMemoryRefreshTokenTests)),
            services =>
            {
                services.AddRouting();
                SpacesTestLicense.RegisterEntitled(services);

                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddStorage(storage => storage.AddSqliteInMemory(dbName))
                    .AddConfigurationStorage()
                    .AddResourceOwnerValidator<AcceptAllPasswordValidator>()
                    // Otherwise data protection alone rejects the token in space B, and the grant store isn't tested.
                    .AddSpaces(spaces => spaces.PartitionDataProtection = false)
                    .AddDeveloperSigningCredential(persistKey: false);
            },
            webapp =>
            {
                webapp.UseSpaceResolution();
                webapp.UseIdentityServer();
            });

        await server.StartAsync();
        await server.GetRequiredService<Duende.Storage.Schema.IStorageInstanceSchema>().MigrateAsync(_ct);

        var spaceAdmin = server.GetRequiredService<ISpaceAdmin>();
        var spaceA = await CreateSpaceAsync(spaceAdmin, "Space A", "/space-a");
        var spaceB = await CreateSpaceAsync(spaceAdmin, "Space B", "/space-b");
        await CreateClientInSpaceAsync(server, spaceA);
        await CreateClientInSpaceAsync(server, spaceB);

        using var http = server.CreateClient();

        var refreshToken = await GetRefreshTokenAsync(http, "/t/space-a");

        var inSpaceB = await RedeemAsync(http, "/t/space-b", refreshToken);
        inSpaceB.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
        (await ReadErrorAsync(inSpaceB)).ShouldBe("invalid_grant");

        var inSpaceA = await RedeemAsync(http, "/t/space-a", refreshToken);
        inSpaceA.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, "the token must still be valid in the space that issued it");
    }

    private async Task<string> GetRefreshTokenAsync(HttpClient http, string prefix)
    {
        using var response = await http.PostAsync($"{prefix}/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["client_secret"] = Secret,
            ["grant_type"] = "password",
            ["username"] = "alice",
            ["password"] = "pw",
            ["scope"] = "api1 offline_access"
        }), _ct);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync(_ct));

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        return json.RootElement.GetProperty("refresh_token").GetString()!;
    }

    private async Task<HttpResponseMessage> RedeemAsync(HttpClient http, string prefix, string refreshToken) =>
        await http.PostAsync($"{prefix}/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["client_secret"] = Secret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        }), _ct);

    private async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        return json.RootElement.GetProperty("error").GetString();
    }

    private async Task<SpaceId> CreateSpaceAsync(ISpaceAdmin spaceAdmin, string name, string path)
    {
        var result = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = name, MatchPatterns = [new SpaceMatchPattern { Path = path }] },
            _ct);
        result.IsSuccess.ShouldBeTrue($"Create {name} failed: {result}");
        return result.Id!.Value;
    }

    private async Task CreateClientInSpaceAsync(KestrelBasedTestServer server, SpaceId spaceId)
    {
        using var scope = server.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var admin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();
        var apiScopeAdmin = scope.ServiceProvider.GetRequiredService<IApiScopeAdmin>();

        using (accessor.SetSpace(spaceId))
        {
            var scopeResult = await apiScopeAdmin.CreateAsync(new CreateApiScope { Name = "api1" }, _ct);
            scopeResult.IsSuccess.ShouldBeTrue($"Create api1 scope failed for space {spaceId}: {scopeResult}");

            var result = await admin.CreateAsync(new CreateClient
            {
                ClientId = ClientId,
                RequireClientSecret = true,
                AllowedGrantTypes = ["password"],
                AllowedScopes = ["api1"],
                AllowOfflineAccess = true,
                ClientSecrets = [new CreateClientSecret { PlaintextValue = Secret }]
            }, _ct);
            result.IsSuccess.ShouldBeTrue($"Create client failed for space {spaceId}: {result}");
        }
    }
}
