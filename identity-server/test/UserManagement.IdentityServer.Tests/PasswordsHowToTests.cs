// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passwords;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentityServer8.UnitTests;

public sealed class PasswordsHowToTests
{
    private static Ct Ct => TestContext.Current.CancellationToken;

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithOptions(WebApplicationBuilder builder)
    {
        // begin-snippet: password-options
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(auth => auth.Configure(o =>
            {
                o.Passwords.MinLength = 15;
                o.Passwords.HistoryCount = 3;
                o.Passwords.MaxAgeDays = 90;
            })));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Configured_password_options_are_applied()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithOptions(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var options = app.Services.GetRequiredService<IOptions<UserAuthenticationOptions>>().Value;

        options.Passwords.MinLength.ShouldBe(15);
        options.Passwords.HistoryCount.ShouldBe(3);
        options.Passwords.MaxAgeDays.ShouldBe(90);
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithCustomValidator(WebApplicationBuilder builder)
    {
        // begin-snippet: password-register-validator
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(auth => auth
                .Configure(o => o.Passwords.MinLength = 15)
                .AddPasswordValidator<RejectDictionaryWordValidator>()));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Custom_validator_rejects_a_password_it_disallows()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithCustomValidator(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var selfService = app.Services.GetRequiredService<IUserAuthenticatorsSelfService>();
        var result = await selfService.TryValidatePasswordAsync(UserSubjectId.New(), "contains-a-password-123", Ct);

        _ = result.ShouldBeOfType<PasswordCreationResult.Failed>();
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithLegacyHashAlgorithm(WebApplicationBuilder builder)
    {
        // begin-snippet: password-register-hash-algorithm
        // Register an additional algorithm so it can still verify (and rehash) passwords it
        // hashed before the switch to the default PBKDF2-SHA512 algorithm. For a full adapter
        // over an existing hash format, see how to import users.
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(auth => auth.AddPasswordHashAlgorithm<LegacyPasswordHashAlgorithm>()));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Registered_hash_algorithm_is_available_alongside_the_default()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithLegacyHashAlgorithm(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var algorithms = app.Services.GetServices<IPasswordHashAlgorithm>();

        algorithms.ShouldContain(a => a.AlgorithmId == LegacyPasswordHashAlgorithm.AlgorithmId);
    }
}

// begin-snippet: password-custom-validator
internal sealed class RejectDictionaryWordValidator : IPasswordValidator
{
    public Task<PasswordValidationResult> ValidateAsync(UserSubjectId userId, string password, Ct ct) =>
        Task.FromResult<PasswordValidationResult>(
            password.Contains("password", StringComparison.OrdinalIgnoreCase)
                ? new PasswordValidationResult.Rejected("Password must not contain a common dictionary word.")
                : new PasswordValidationResult.Accepted());
}
// end-snippet

// begin-snippet: password-legacy-hash-algorithm
internal sealed class LegacyPasswordHashAlgorithm : IPasswordHashAlgorithm
{
    public const string AlgorithmId = "legacy-sha256";

    string IPasswordHashAlgorithm.AlgorithmId => AlgorithmId;

    public HashedPasswordData Hash(string password) =>
        new(AlgorithmId, System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password)), [], new Dictionary<string, string>());

    public bool Verify(string password, HashedPasswordData data) =>
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password)).SequenceEqual([.. data.Hash]);

    public bool NeedsRehash(HashedPasswordData data) => data.AlgorithmId != AlgorithmId;
}
// end-snippet
