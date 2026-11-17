// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.UserManagement;
using Duende.Storage;
using Duende.IdentityServer.DataExtensions;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer8.UnitTests;

public sealed class AddUserManagementHowToTests
{
    private static Ct Ct => TestContext.Current.CancellationToken;

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void Register(WebApplicationBuilder builder)
    {
        // begin-snippet: add-um-identityserver-register
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(
                auth => auth.Configure(o => o.Passwords.MinLength = 15)));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Registering_resolves_IUserAdmin_and_issues_claims_through_UserManagementProfileService()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        Register(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        _ = app.Services.GetRequiredService<IUserAdmin>();
        var profileService = app.Services.GetRequiredService<IProfileService>();
        _ = profileService.ShouldBeOfType<UserManagementProfileService>();
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterNamedInstance(WebApplicationBuilder builder)
    {
        // begin-snippet: add-um-identityserver-named-instance
        var userManagementInstance = StorageInstanceId.Create("user-management");
        _ = builder.Services.AddIdentityServer()
            .AddStorage(userManagementInstance, storage => storage.AddSqliteInMemory())
            .AddUserManagement(userManagementInstance, users => users.Authentication(
                auth => auth.Configure(o => o.Passwords.MinLength = 15)));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Data_lands_in_the_named_storage_instance()
    {
        var instance = StorageInstanceId.Create("user-management");
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterNamedInstance(builder);

        await using var app = builder.Build();
        var schemaFactory = app.Services.GetRequiredService<IStorageInstanceSchemaFactory>();
        await (await schemaFactory.GetStorageInstanceSchema(instance, Ct)).MigrateAsync(Ct);

        var profileAdmin = app.Services.GetRequiredService<IUserProfileAdmin>();
        var subjectId = (await app.Services.GetRequiredService<IExternalAuthenticator>()
                .TryAuthenticateAsync(
                    new ExternalAuthenticatorAddress(
                        ExternalAuthenticatorName.Create("test-ext"), OpaqueSubjectId.Create(Guid.NewGuid().ToString())),
                    Ct))
            .ShouldBeOfType<ExternalAuthenticationResult.Success>().UserSubjectId;

        var attributes = new AttributeValueCollection(await profileAdmin.GetSchemaAsync(Ct));
        attributes.Set(AttributeCode.Create("email"), "frank@example.com");
        _ = (await profileAdmin.TryAddAsync(subjectId, attributes.Validate(), Ct)).ShouldNotBeNull();

        var found = await profileAdmin.TryGetAsync(subjectId, Ct);
        _ = found.ShouldNotBeNull();
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static async Task MigrateAsync(WebApplication app, Ct ct)
    {
        // begin-snippet: add-um-migrate
        var schema = app.Services.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task MigrateAsync_snippet_creates_the_schema()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        Register(builder);

        await using var app = builder.Build();
        await MigrateAsync(app, Ct);

        _ = app.Services.GetRequiredService<IUserAdmin>();
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void MapEndpoints(WebApplication app)
    {
        // begin-snippet: add-um-map-endpoints
        _ = app.MapUserManagement();
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Passkeys_js_is_served_once_MapUserManagement_is_called()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        Register(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        MapEndpoints(app);

        await app.StartAsync(Ct);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/passkeys/js", Ct);
        _ = response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_custom_IProfileService_registered_before_AddUserManagement_is_kept()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        _ = builder.Services.AddSingleton<IProfileService, CustomProfileService>();
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(_ => { });

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        _ = app.Services.GetRequiredService<IProfileService>().ShouldBeOfType<CustomProfileService>();
    }

    private sealed class CustomProfileService : IProfileService
    {
        public Task GetProfileDataAsync(ProfileDataRequestContext context, Ct ct) => Task.CompletedTask;

        public Task IsActiveAsync(IsActiveContext context, Ct ct)
        {
            context.IsActive = true;
            return Task.CompletedTask;
        }
    }
}
