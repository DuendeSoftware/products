// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Platform.UserManagement.Import;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Import;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer8.UnitTests;

public sealed class ImportUsersHowToTests
{
    private static Ct Ct => TestContext.Current.CancellationToken;

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithLegacyHashAlgorithm(WebApplicationBuilder builder)
    {
        // begin-snippet: import-register-legacy-hash
        // Register the adapter so imported ASP.NET Identity hashes can still be verified (and
        // rehashed on first sign-in) after import. See AspNetIdentityPasswordHashAlgorithm below.
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(
                auth => auth.AddPasswordHashAlgorithm<AspNetIdentityPasswordHashAlgorithm>()));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Registered_legacy_hash_algorithm_is_available_alongside_the_default()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithLegacyHashAlgorithm(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var algorithms = app.Services.GetServices<IPasswordHashAlgorithm>();

        algorithms.ShouldContain(a => a.AlgorithmId == AspNetIdentityPasswordHashAlgorithm.Id);
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithConflictResolver(WebApplicationBuilder builder)
    {
        // begin-snippet: import-register-conflict-resolver
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(
                auth => auth.Configure(o => o.Passwords.MinLength = 15)));

        // Replace the default (skip-on-duplicate) resolver with one that overwrites. See
        // OverwriteOnDuplicateResolver in how to import users, "Resolve conflicts".
        _ = builder.Services.AddSingleton<IUserImportConflictResolver, OverwriteOnDuplicateResolver>();
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Registered_conflict_resolver_replaces_the_default()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithConflictResolver(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var resolver = app.Services.GetRequiredService<IUserImportConflictResolver>();

        _ = resolver.ShouldBeOfType<OverwriteOnDuplicateResolver>();
    }

    /// <summary>
    /// Stand-in for the resolver class a reader writes (shown, with its registration, in
    /// IdentityServer8.UnitTests.ImportUsersHowToTests.RegisterWithConflictResolver above). The
    /// snippet-wrapped class lives in UserManagement.Tests.Public.ImportUsersHowToTests, which has no
    /// InternalsVisibleTo access from this project.
    /// </summary>
    private sealed class OverwriteOnDuplicateResolver : IUserImportConflictResolver
    {
        public Task<UserImportConflictResolution> ResolveAsync(UserImportConflict conflict, Ct ct) =>
            Task.FromResult<UserImportConflictResolution>(conflict switch
            {
                { Reason: UserImportConflictReason.ConcurrencyConflict } => new UserImportConflictResolution.Retry(),
                { Step: UserImportStep.UserRecord } => new UserImportConflictResolution.Overwrite(conflict.Record.SubjectId),
                _ => new UserImportConflictResolution.Skip()
            });
    }
}
