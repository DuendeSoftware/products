// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.Storage;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer8.UnitTests;

/// <summary>
/// Shared, non-snippet host for the how-to test classes in <c>IdentityServer8.UnitTests</c>. Builds a
/// <see cref="WebApplication"/> wired up with <c>UseTestServer()</c>,
/// <c>AddIdentityServer().AddStorage(s =&gt; s.AddSqliteInMemory()).AddUserManagement(...)</c>, migrated and
/// started, using only the public API a reader of the how-tos can call. Every how-to that extends registration
/// (the user profiles, passwords, OTP, passkeys, import, SCIM and second-factor how-tos) builds its own host
/// through this helper instead of repeating IdentityServer/TestServer plumbing.
/// </summary>
public sealed class HowToIdentityServerHost : IAsyncDisposable
{
    private HowToIdentityServerHost(WebApplication app) => App = app;

    /// <summary>
    /// The underlying <see cref="WebApplication"/>, for anything the options below don't expose directly
    /// (for example mapping additional test-only endpoints).
    /// </summary>
    public WebApplication App { get; }

    /// <summary>The app's root <see cref="IServiceProvider"/>.</summary>
    public IServiceProvider Services => App.Services;

    /// <summary>
    /// Builds, migrates and starts an IdentityServer host with User Management added, over a <see cref="TestServer"/>.
    /// </summary>
    /// <param name="configureUserManagement">
    /// The callback passed to <c>AddUserManagement(...)</c>. This is where a how-to's own registration snippet
    /// (password options, OTP dispatcher, passkey routes, and so on) plugs in.
    /// </param>
    /// <param name="storageInstanceId">
    /// The storage instance that holds User Management's (and, by default, IdentityServer's) data.
    /// Defaults to <see cref="StorageInstanceId.Default"/>.
    /// </param>
    /// <param name="configureServicesBeforeUserManagement">
    /// Runs against <see cref="WebApplicationBuilder.Services"/> before <c>AddIdentityServer()</c>/<c>AddUserManagement(...)</c>,
    /// for registrations that must exist first, such as a custom <c>IProfileService</c> that
    /// <c>AddUserManagement</c> should preserve.
    /// </param>
    /// <param name="configureIdentityServer">
    /// Runs against the <see cref="IIdentityServerBuilder"/> after <c>AddStorage(...)</c> and before
    /// <c>AddUserManagement(...)</c>, for example to call <c>AddConfigurationStorage()</c>/<c>AddOperationalStorage()</c>
    /// or set <see cref="IdentityServerOptions.IssuerUri"/>.
    /// </param>
    /// <param name="configureIdentityServerOptions">Passed to <c>AddIdentityServer(options =&gt; ...)</c>.</param>
    /// <param name="configureServicesAfterUserManagement">
    /// Runs against <see cref="WebApplicationBuilder.Services"/> after <c>AddUserManagement(...)</c>, for
    /// registrations that must come last, such as overriding a service User Management just registered.
    /// </param>
    /// <param name="configureApp">
    /// Runs against the built <see cref="WebApplication"/> before it starts, for pipeline setup
    /// (for example <c>app.MapUserManagement()</c>) and additional test-only endpoints.
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    public static async Task<HowToIdentityServerHost> CreateAsync(
        Action<IUserManagementBuilder> configureUserManagement,
        StorageInstanceId? storageInstanceId = null,
        Action<IServiceCollection>? configureServicesBeforeUserManagement = null,
        Action<IIdentityServerBuilder>? configureIdentityServer = null,
        Action<IdentityServerOptions>? configureIdentityServerOptions = null,
        Action<IServiceCollection>? configureServicesAfterUserManagement = null,
        Action<WebApplication>? configureApp = null,
        Ct ct = default)
    {
        var instanceId = storageInstanceId ?? StorageInstanceId.Default;

        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();

        configureServicesBeforeUserManagement?.Invoke(builder.Services);

        var identityServerBuilder = builder.Services
            .AddIdentityServer(configureIdentityServerOptions ?? (_ => { }))
            .AddStorage(instanceId, storage => storage.AddSqliteInMemory());

        configureIdentityServer?.Invoke(identityServerBuilder);

        _ = identityServerBuilder.AddUserManagement(instanceId, configureUserManagement);

        configureServicesAfterUserManagement?.Invoke(builder.Services);

        var app = builder.Build();
        configureApp?.Invoke(app);

        var schemaFactory = app.Services.GetRequiredService<IStorageInstanceSchemaFactory>();
        var schema = await schemaFactory.GetStorageInstanceSchema(instanceId, ct);
        await schema.MigrateAsync(ct);

        await app.StartAsync(ct);

        return new HowToIdentityServerHost(app);
    }

    /// <summary>A test <see cref="HttpClient"/> for the app, from <see cref="TestServer.CreateClient"/>.</summary>
    public HttpClient CreateClient() => App.GetTestClient();

    public async ValueTask DisposeAsync() => await App.DisposeAsync();
}
