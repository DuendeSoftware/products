// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passwords;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class LegacyPasswordRehashing
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task authenticate_with_legacy_algorithm_succeeds_and_rehashes()
    {
        var dbId = Guid.NewGuid();

        var externalAuth = TestData.CreateExternalAuthenticator();
        var userName = TestData.CreateUserName();
        var passwordText = $"ABcd12!@{Guid.NewGuid()}";

        // Seed: set password using fake algorithm as preferred
        await using var seedProvider = await CreateProviderWithFakeAlgorithmAsPreferred(dbId);

        var selfService = seedProvider.GetRequiredService<IUserAuthenticatorsSelfService>();

        var authenticators = (await selfService.TryRegisterAsync(UserSubjectId.New(), externalAuth, _ct)).ShouldNotBeNull();
        var userSelfService = seedProvider.GetRequiredService<IUserSelfService>();
        (await userSelfService.TrySetUserNameAsync(authenticators.SubjectId, userName, _ct)).ShouldBeTrue();
        var password = await selfService.CreatePasswordAsync(authenticators.SubjectId, passwordText, _ct);
        (await selfService.TrySetPasswordAsync(authenticators.SubjectId, password, _ct)).ShouldBeTrue();

        // Authenticate: use PBKDF2 as preferred — should succeed and re-hash
        await using (var authProvider = await CreateProviderWithPbkdf2AsPreferredAndFakeRegistered(dbId))
        {
            var auth = authProvider.GetRequiredService<IPasswordAuth>();
            var supplied = NonValidatedPassword.Create(passwordText);

            var result = await auth.TryAuthenticateAsync(userName, supplied, _ct);

            _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
        }

        // Verify re-hash: authenticate again with only PBKDF2 (fake no longer needed)
        await using (var verifyProvider = await CreateProviderWithPbkdf2Only(dbId))
        {
            var auth = verifyProvider.GetRequiredService<IPasswordAuth>();
            var supplied = NonValidatedPassword.Create(passwordText);

            var result = await auth.TryAuthenticateAsync(userName, supplied, _ct);

            _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
        }
    }

    [Fact]
    public async Task custom_algorithm_registered_in_di_can_hash_and_verify()
    {
        await using var sp = await UsersServiceProviderFactory
            .CreateUsersBuilderAsync(
                configureOptions: options => options.Passwords.PreferredHashAlgorithm = FakePasswordHashAlgorithm.AlgorithmId,
                addDataProtection: false,
                configureServices: services => _ = services.AddSingleton<IPasswordHashAlgorithm, FakePasswordHashAlgorithm>());

        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();
        var auth = sp.GetRequiredService<IPasswordAuth>();

        var externalAuth = TestData.CreateExternalAuthenticator();
        var userName = TestData.CreateUserName();
        var rawPassword = $"ABcd12!@{Guid.NewGuid()}";
        var supplied = NonValidatedPassword.Create(rawPassword);

        var authenticators = (await selfService.TryRegisterAsync(UserSubjectId.New(), externalAuth, _ct)).ShouldNotBeNull();
        var userSelfService = sp.GetRequiredService<IUserSelfService>();
        (await userSelfService.TrySetUserNameAsync(authenticators.SubjectId, userName, _ct)).ShouldBeTrue();
        var password = await selfService.CreatePasswordAsync(authenticators.SubjectId, rawPassword, _ct);
        (await selfService.TrySetPasswordAsync(authenticators.SubjectId, password, _ct)).ShouldBeTrue();

        var result = await auth.TryAuthenticateAsync(userName, supplied, _ct);

        var success = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
        success.UserSubjectId.ShouldBe(authenticators.SubjectId);
    }

    [Fact]
    public async Task authenticate_with_unregistered_algorithm_returns_null()
    {
        var dbId = Guid.NewGuid();

        var externalAuth = TestData.CreateExternalAuthenticator();
        var userName = TestData.CreateUserName();
        var passwordText = $"ABcd12!@{Guid.NewGuid()}";

        // Seed: set password using fake algorithm as preferred
        await using (var seedProvider = await CreateProviderWithFakeAlgorithmAsPreferred(dbId))
        {
            var selfService = seedProvider.GetRequiredService<IUserAuthenticatorsSelfService>();

            var authenticators = (await selfService.TryRegisterAsync(UserSubjectId.New(), externalAuth, _ct)).ShouldNotBeNull();
            var userSelfService = seedProvider.GetRequiredService<IUserSelfService>();
            (await userSelfService.TrySetUserNameAsync(authenticators.SubjectId, userName, _ct)).ShouldBeTrue();
            var password = await selfService.CreatePasswordAsync(authenticators.SubjectId, passwordText, _ct);
            (await selfService.TrySetPasswordAsync(authenticators.SubjectId, password, _ct)).ShouldBeTrue();
        }

        // Authenticate: only PBKDF2 registered — fake algorithm unknown
        await using (var authProvider = await CreateProviderWithPbkdf2Only(dbId))
        {
            var auth = authProvider.GetRequiredService<IPasswordAuth>();
            var supplied = NonValidatedPassword.Create(passwordText);

            var result = await auth.TryAuthenticateAsync(userName, supplied, _ct);

            _ = result.ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        }
    }

    private static async Task<ServiceProvider> CreateProviderWithFakeAlgorithmAsPreferred(Guid dbId) =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: options => options.Passwords.PreferredHashAlgorithm = FakePasswordHashAlgorithm.AlgorithmId,
            addDataProtection: true,
            configureServices: services => _ = services.AddSingleton<IPasswordHashAlgorithm, FakePasswordHashAlgorithm>(),
            dbId: dbId);

    private static async Task<ServiceProvider> CreateProviderWithPbkdf2AsPreferredAndFakeRegistered(Guid dbId) =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null,
            addDataProtection: true,
            configureServices: services => _ = services.AddSingleton<IPasswordHashAlgorithm, FakePasswordHashAlgorithm>(),
            dbId: dbId);

    private static async Task<ServiceProvider> CreateProviderWithPbkdf2Only(Guid dbId) =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null,
            addDataProtection: true,
            dbId: dbId);

    private sealed class FakePasswordHashAlgorithm : IPasswordHashAlgorithm
    {
        public const string AlgorithmId = "fake-test-algo";

        string IPasswordHashAlgorithm.AlgorithmId => AlgorithmId;

        public HashedPasswordData Hash(string password) =>
            new(AlgorithmId,
                System.Text.Encoding.UTF8.GetBytes(password),
                [],
                new Dictionary<string, string>());

        public bool Verify(string password, HashedPasswordData data) =>
            System.Text.Encoding.UTF8.GetBytes(password).SequenceEqual([.. data.Hash]);

        public bool NeedsRehash(HashedPasswordData data) =>
            data.AlgorithmId != AlgorithmId;
    }

}
