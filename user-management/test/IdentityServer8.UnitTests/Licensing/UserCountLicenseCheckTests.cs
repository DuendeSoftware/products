// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityServer;
using Duende.Spaces;
using Duende.Spaces.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.TestIsolation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace IdentityServer8.UnitTests.Licensing;

public sealed class UserCountLicenseCheckTests(WebServerFixture testServer) : IAsyncDisposable
{
    private const int UserLimit = 2;

    private static Ct Ct => TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private FakeLogCollector _licenseLogs = null!;
    private KestrelBasedTestServer _identityServer = null!;

    // Disposing the server disposes its service provider, and with it the license validator,
    // which cancels and drains any running count check.
    public async ValueTask DisposeAsync()
    {
        if (_identityServer is not null)
        {
            await _identityServer.DisposeAsync();
        }
    }

    [Fact]
    public async Task user_created_in_one_space_validates_total_across_all_spaces()
    {
        _identityServer = new KestrelBasedTestServer(
            serverName: "identity-server",
            fixture: testServer,
            output: TestContext.Current.TestOutputHelper!,
            configureServices: services =>
            {
                _ = services.AddFakeLogging();
                _ = services.AddLogging(logging =>
                {
                    _ = logging.SetMinimumLevel(LogLevel.Debug);
                    _ = logging.AddXUnit(TestContext.Current.TestOutputHelper!);
                });
                _ = services.AddIdentityServer()
                    .AddStorage(x => x.AddSqliteInMemory())
                    .AddConfigurationStorage()
                    .AddOperationalStorage()
                    .AddUserManagement(_ => { });
                _ = services.AddSpacesInternal();
                _ = services.RemoveAll<TimeProvider>();
                _ = services.AddSingleton<TimeProvider>(_time);
                _licenseLogs = TestUserManagementLicense.Create().WithUserLimit(UserLimit).Register(services, _time);
            },
            configurePipeline: webapp =>
            {
                _ = webapp.UseSpaceResolution();
                _ = webapp.MapGet("/users/create", async (IExternalAuthenticator authenticator) =>
                {
                    _ = (await authenticator.TryAuthenticateAsync(NewExternalAddress(), Ct))
                        .ShouldBeOfType<ExternalAuthenticationResult.Success>();
                    return Results.Ok();
                });
            });
        await _identityServer.StartAsync();
        await _identityServer.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var tenantA = await CreateSpaceAsync("tenant-a");
        var tenantB = await CreateSpaceAsync("tenant-b");
        await CreateUsersAsync(SpaceId.Default, 4);
        await CreateUsersAsync(tenantA.Id, 4);
        await StartNextCheckWindowAsync();

        using var http = _identityServer.CreateClient();
        using var response = await http.GetAsync(new Uri(tenantB.Origin, "/users/create"), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CountUsersAsync(tenantB.Id)).ShouldBe(1);
        await _licenseLogs.WaitForLogAsync(
            r => r.Level >= LogLevel.Warning && r.Message.Contains("User Count (9)", StringComparison.Ordinal), Ct);
    }

    private async Task<(SpaceId Id, Uri Origin)> CreateSpaceAsync(string name)
    {
        var host = $"{_identityServer.TestId}-{name}.dev.localhost";
        _identityServer.RegisterHostAlias(host);
        var origin = new Uri($"https://{host}:{_identityServer.BaseAddress.Port}");

        var created = await _identityServer.GetRequiredService<ISpaceAdmin>().CreateAsync(new CreateSpaceConfiguration
        {
            Name = name,
            MatchPatterns = [new SpaceMatchPattern { Origin = origin.GetLeftPart(UriPartial.Authority) }]
        }, Ct);
        created.IsSuccess.ShouldBeTrue($"Create failed: {created}");
        return (created.Id, origin);
    }

    private async Task CreateUsersAsync(SpaceId spaceId, int count)
    {
        using var space = _identityServer.GetRequiredService<ISpaceContextAccessor>().SetSpace(spaceId);
        for (var i = 0; i < count; i++)
        {
            await using var scope = _identityServer.Services.CreateAsyncScope();
            _ = (await scope.ServiceProvider.GetRequiredService<IExternalAuthenticator>().TryAuthenticateAsync(NewExternalAddress(), Ct))
                .ShouldBeOfType<ExternalAuthenticationResult.Success>();
        }
    }

    private async Task<long> CountUsersAsync(SpaceId spaceId)
    {
        using var space = _identityServer.GetRequiredService<ISpaceContextAccessor>().SetSpace(spaceId);
        await using var scope = _identityServer.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserRepository>().GetCountAsync(Ct);
    }

    // Seeding triggers a check of its own, so let it finish and move past its window before the request under test.
    private async Task StartNextCheckWindowAsync()
    {
        await _identityServer.GetRequiredService<FakeLogCollector>()
            .WaitForLogAsync(r => r.Id.Name == "UserCountCheckCompleted", Ct);
        _licenseLogs.Clear();
        _time.Advance(TimeSpan.FromHours(2));
    }

    private static ExternalAuthenticatorAddress NewExternalAddress() =>
        new(ExternalAuthenticatorName.Create("test-ext"), OpaqueSubjectId.Create(Guid.NewGuid().ToString()));
}
