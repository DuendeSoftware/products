// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityModel;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Stores.Storage.PushedAuthorization;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Authorize;

/// <summary>
/// Runs pushed authorization protocol tests against IPartitionedStorage-backed operational
/// stores using an in-memory SQLite database.
/// </summary>
public sealed class PushedAuthorizationTests_PartitionedStorage : PushedAuthorizationTestsBase, IAsyncLifetime
{
    public PushedAuthorizationTests_PartitionedStorage()
    {
        _mockPipeline.OnPostConfigureServices += services => services.AddOperationalStorageForTesting();
        InitializePipeline();
    }

    public async ValueTask InitializeAsync() =>
        await _mockPipeline.MigrateStorageSchemaAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task pushed_authorization_request_is_persisted_in_storage()
    {
        var (response, statusCode) = await _mockPipeline.PushAuthorizationRequestAsync();
        using var parJson = response;
        statusCode.ShouldBe(HttpStatusCode.Created);

        var requestUri = parJson.RootElement.GetProperty("request_uri").GetString();
        var prefix = $"{IdentityServerConstants.PushedAuthorizationRequestUri}:";
        requestUri.ShouldStartWith(prefix);
        var referenceValue = requestUri[prefix.Length..];

        await using var scope = _mockPipeline.ApplicationServices.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<PushedAuthorizationRepository>();
        var storedRequest = await repository.TryReadByHashAsync(referenceValue.ToSha256(), TestContext.Current.CancellationToken);
        storedRequest.ShouldNotBeNull();
    }
}

