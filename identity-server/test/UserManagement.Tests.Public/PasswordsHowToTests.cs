// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.Models;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class PasswordsHowToTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IUserProfileAdmin _profileAdmin = null!;
    private IUserAuthenticatorsAdmin _authenticatorsAdmin = null!;
    private IUserAdmin _userAdmin = null!;
    private IUserAuthenticatorsSelfService _selfService = null!;
    private IPasswordAuthenticator _auth = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _profileAdmin = _serviceProvider.GetRequiredService<IUserProfileAdmin>();
        _authenticatorsAdmin = _serviceProvider.GetRequiredService<IUserAuthenticatorsAdmin>();
        _userAdmin = _serviceProvider.GetRequiredService<IUserAdmin>();
        _selfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _auth = _serviceProvider.GetRequiredService<IPasswordAuthenticator>();

        await RegisterEmailSchemaAsync(_serviceProvider.GetRequiredService<ISchemaAdmin>(), _ct);
    }

    private static async Task RegisterEmailSchemaAsync(ISchemaAdmin schemaAdmin, Ct ct)
    {
        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, ct);
        var schema = getResult.Found
            ? getResult.Item!
            : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };

        schema.AttributeDefinitions.Add(OidcStandardAttributes.Email with { IsUnique = true });

        var saveResult = getResult.Found
            ? await schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, ct)
            : await schemaAdmin.CreateAsync(schema, ct);

        saveResult.IsSuccess.ShouldBeTrue();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static async Task<UserSubjectId?> CreateUserWithPassword(
        IUserAdmin userAdmin,
        IUserAuthenticatorsSelfService selfService,
        string email,
        string passwordText,
        Ct ct)
    {
        // begin-snippet: password-create-user
        var subjectId = UserSubjectId.New();

        // Validate the password before writing anything. TryValidatePasswordAsync only checks complexity
        // and password history, and the history check treats a subject with no stored authenticators yet
        // (as here, before the profile is even created) as having no history to match, so it's safe to
        // call before the user exists.
        if (await selfService.TryValidatePasswordAsync(subjectId, passwordText, ct) is not PasswordCreationResult.Success success)
        {
            return null;
        }

        var attributes = new AttributeValueCollection(await userAdmin.Profiles.GetSchemaAsync(ct));
        attributes.Set(AttributeCode.Create("email"), email);
        if (await userAdmin.Profiles.TryAddAsync(subjectId, attributes.Validate(), ct) is null)
        {
            // Another user already has this email; stop here rather than leave a half-created user.
            return null;
        }

        // A password is itself an authenticator. If the user has no other authenticator yet,
        // create the (otherwise empty) authenticator record first. Check both writes: if either
        // fails, the user would be left with a reserved email and no usable password, so remove
        // everything we just created instead of reporting success.
        var authenticatorsCreated = await userAdmin.Authenticators.TryAddAsync(subjectId, [], [], ct) is not null;
        var passwordSet = authenticatorsCreated && await selfService.TrySetPasswordAsync(subjectId, success.Password, ct);
        if (!passwordSet)
        {
            _ = await userAdmin.TryRemoveAsync(subjectId, ct);
            return null;
        }
        // end-snippet

        return subjectId;
    }

    private static async Task<PasswordAuthenticationResult> SignIn(
        IPasswordAuthenticator auth, string email, string suppliedPasswordText, Ct ct)
    {
        // begin-snippet: password-sign-in
        var result = await auth.TryAuthenticateAsync(
            AttributeCode.Create("email"), email, NonValidatedPassword.Create(suppliedPasswordText), ct);
        // end-snippet

        return result;
    }

    private static async Task<bool> ChangePassword(
        IUserAuthenticatorsSelfService selfService,
        UserSubjectId subjectId,
        string oldPasswordText,
        string newPasswordText,
        Ct ct)
    {
        // begin-snippet: password-change
        var newPassword = await selfService.ValidatePasswordAsync(subjectId, newPasswordText, ct);
        var changed = await selfService.TryChangePasswordAsync(
            subjectId, NonValidatedPassword.Create(oldPasswordText), newPassword, ct);
        // end-snippet

        return changed;
    }

    private static async Task<bool> ResetPassword(
        IUserAuthenticatorsSelfService selfService, UserSubjectId subjectId, string newPasswordText, Ct ct)
    {
        // begin-snippet: password-reset
        var newPassword = await selfService.ValidatePasswordAsync(subjectId, newPasswordText, ct);
        var reset = await selfService.TryResetPasswordAsync(subjectId, newPassword, ct);
        // end-snippet

        return reset;
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Created_user_can_sign_in_with_the_password_that_was_set()
    {
        var email = $"frank-{Guid.NewGuid():N}@example.com";
        const string passwordText = "correct-horse-battery-staple-1";

        _ = await CreateUserWithPassword(_userAdmin, _selfService, email, passwordText, _ct);

        var result = await SignIn(_auth, email, passwordText, _ct);

        _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Sign_in_with_the_wrong_password_fails()
    {
        var email = $"frank-{Guid.NewGuid():N}@example.com";
        _ = await CreateUserWithPassword(_userAdmin, _selfService, email, "correct-horse-battery-staple-1", _ct);

        var result = await SignIn(_auth, email, "wrong-horse-battery-staple-1", _ct);

        _ = result.ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    [Fact]
    public async Task Changing_the_password_requires_the_current_password()
    {
        var email = $"frank-{Guid.NewGuid():N}@example.com";
        const string oldPasswordText = "correct-horse-battery-staple-1";
        const string newPasswordText = "correct-horse-battery-staple-2";
        var subjectId = (await CreateUserWithPassword(_userAdmin, _selfService, email, oldPasswordText, _ct)).ShouldNotBeNull();

        (await ChangePassword(_selfService, subjectId, "wrong-current-password-123", newPasswordText, _ct)).ShouldBeFalse();
        (await ChangePassword(_selfService, subjectId, oldPasswordText, newPasswordText, _ct)).ShouldBeTrue();

        _ = (await SignIn(_auth, email, oldPasswordText, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await SignIn(_auth, email, newPasswordText, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Resetting_the_password_does_not_require_the_current_password()
    {
        var email = $"frank-{Guid.NewGuid():N}@example.com";
        const string oldPasswordText = "correct-horse-battery-staple-1";
        const string newPasswordText = "correct-horse-battery-staple-3";
        var subjectId = (await CreateUserWithPassword(_userAdmin, _selfService, email, oldPasswordText, _ct)).ShouldNotBeNull();

        (await ResetPassword(_selfService, subjectId, newPasswordText, _ct)).ShouldBeTrue();

        _ = (await SignIn(_auth, email, oldPasswordText, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await SignIn(_auth, email, newPasswordText, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Creating_a_second_user_with_an_already_registered_email_fails_and_leaves_no_authenticator()
    {
        var email = $"frank-{Guid.NewGuid():N}@example.com";
        const string passwordText = "correct-horse-battery-staple-5";
        _ = (await CreateUserWithPassword(_userAdmin, _selfService, email, passwordText, _ct)).ShouldNotBeNull();

        var secondSubjectId = await CreateUserWithPassword(_userAdmin, _selfService, email, "another-horse-battery-staple-5", _ct);

        secondSubjectId.ShouldBeNull();
    }

    [Fact]
    public async Task A_rejected_password_does_not_create_a_user_and_a_retry_with_a_valid_password_succeeds()
    {
        var email = $"frank-{Guid.NewGuid():N}@example.com";
        const string tooShortPasswordText = "short1";
        const string validPasswordText = "correct-horse-battery-staple-6";

        var rejected = await CreateUserWithPassword(_userAdmin, _selfService, email, tooShortPasswordText, _ct);
        rejected.ShouldBeNull();
        (await _profileAdmin.TryGetAsync(AttributeCode.Create("email"), email, _ct)).ShouldBeNull();

        var subjectId = (await CreateUserWithPassword(_userAdmin, _selfService, email, validPasswordText, _ct)).ShouldNotBeNull();

        var result = await SignIn(_auth, email, validPasswordText, _ct);
        _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task A_failed_authenticator_write_cleans_up_the_profile_and_does_not_report_success()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null,
            addDataProtection: false,
            configureServices: services => services.Replace(
                ServiceDescriptor.Transient<IUserAuthenticatorsAdmin, AlwaysFailingAuthenticatorsAdmin>()));

        var userAdmin = serviceProvider.GetRequiredService<IUserAdmin>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var profileAdmin = serviceProvider.GetRequiredService<IUserProfileAdmin>();
        await RegisterEmailSchemaAsync(serviceProvider.GetRequiredService<ISchemaAdmin>(), _ct);

        var email = $"frank-{Guid.NewGuid():N}@example.com";

        var subjectId = await CreateUserWithPassword(userAdmin, selfService, email, "correct-horse-battery-staple-7", _ct);

        subjectId.ShouldBeNull();
        (await profileAdmin.TryGetAsync(AttributeCode.Create("email"), email, _ct)).ShouldBeNull();
    }

    /// <summary>
    /// Wraps the real <see cref="IUserAuthenticatorsAdmin"/> but forces <see cref="TryAddAsync"/> to report
    /// failure, to exercise the password-create-user snippet's cleanup when the authenticator write fails.
    /// </summary>
    /// <summary>
    /// Always fails authenticator creation, to exercise the password-create-user snippet's cleanup
    /// when the authenticator write fails. The other members aren't exercised by this test.
    /// </summary>
    private sealed class AlwaysFailingAuthenticatorsAdmin : IUserAuthenticatorsAdmin
    {
        public Task<UserAuthenticators?> TryAddAsync(
            UserSubjectId subjectId,
            IEnumerable<OtpAddress> otpAddresses,
            IEnumerable<ExternalAuthenticatorAddress> externalAuthenticatorAddresses,
            Ct ct) => Task.FromResult<UserAuthenticators?>(null);

        public Task<UserAuthenticators?> TryGetAsync(UserSubjectId subjectId, Ct ct) =>
            throw new NotSupportedException();

        public Task<bool> TryAddOtpAddressesAsync(UserSubjectId subjectId, IEnumerable<OtpAddress> addresses, Ct ct) =>
            throw new NotSupportedException();

        public Task<bool> TryRemoveOtpAddressesAsync(UserSubjectId subjectId, IEnumerable<OtpAddress> addresses, Ct ct) =>
            throw new NotSupportedException();

        public Task<bool> TryAddExternalAuthenticatorAddressesAsync(UserSubjectId subjectId, IEnumerable<ExternalAuthenticatorAddress> addresses, Ct ct) =>
            throw new NotSupportedException();

        public Task<bool> TryRemoveExternalAuthenticatorAddressesAsync(UserSubjectId subjectId, IEnumerable<ExternalAuthenticatorAddress> addresses, Ct ct) =>
            throw new NotSupportedException();

        public Task<QueryResult<UserAuthenticators>> QueryAsync(QueryRequest request, Ct ct) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task Signing_in_with_a_legacy_hash_rehashes_it_to_the_preferred_algorithm()
    {
        var dbId = Guid.NewGuid();
        var email = $"frank-{Guid.NewGuid():N}@example.com";
        const string passwordText = "correct-horse-battery-staple-4";

        // Seed: the legacy algorithm is preferred, so the password is hashed with it. Kept open for
        // the duration of the test: the shared in-memory SQLite database needs at least one open
        // connection to survive across the ServiceProvider instances below.
        await using var seedProvider = await CreateProviderPreferringLegacyAlgorithm(dbId);

        await RegisterEmailSchemaAsync(seedProvider.GetRequiredService<ISchemaAdmin>(), _ct);

        var externalAuthenticator = seedProvider.GetRequiredService<IExternalAuthenticator>();
        var profileAdmin = seedProvider.GetRequiredService<IUserProfileAdmin>();
        var selfService = seedProvider.GetRequiredService<IUserAuthenticatorsSelfService>();

        var subjectId = await externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);

        var attributes = new AttributeValueCollection(await profileAdmin.GetSchemaAsync(_ct));
        attributes.Set(AttributeCode.Create("email"), email);
        _ = await profileAdmin.TryAddAsync(subjectId, attributes.Validate(), _ct);

        var password = await selfService.ValidatePasswordAsync(subjectId, passwordText, _ct);
        (await selfService.TrySetPasswordAsync(subjectId, password, _ct)).ShouldBeTrue();

        // Sign in: the default (preferred) algorithm is PBKDF2-SHA512, with the legacy algorithm
        // still registered so it can verify the existing hash. Authentication succeeds and rehashes.
        await using (var authProvider = await CreateProviderWithLegacyAlgorithmRegistered(dbId))
        {
            var result = await SignIn(authProvider.GetRequiredService<IPasswordAuthenticator>(), email, passwordText, _ct);

            _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
        }

        // Verify: sign in again with only the preferred algorithm registered. The legacy
        // algorithm is no longer needed because the stored hash was rehashed above.
        await using (var verifyProvider = await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null, addDataProtection: true, dbId: dbId))
        {
            var result = await SignIn(verifyProvider.GetRequiredService<IPasswordAuthenticator>(), email, passwordText, _ct);

            _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
        }
    }

    private static async Task<ServiceProvider> CreateProviderPreferringLegacyAlgorithm(Guid dbId) =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: options => options.Passwords.PreferredHashAlgorithm = FixtureLegacyPasswordHashAlgorithm.AlgorithmId,
            addDataProtection: true,
            configureServices: services => _ = services.AddSingleton<IPasswordHashAlgorithm, FixtureLegacyPasswordHashAlgorithm>(),
            dbId: dbId);

    private static async Task<ServiceProvider> CreateProviderWithLegacyAlgorithmRegistered(Guid dbId) =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null,
            addDataProtection: true,
            configureServices: services => _ = services.AddSingleton<IPasswordHashAlgorithm, FixtureLegacyPasswordHashAlgorithm>(),
            dbId: dbId);

    /// <summary>
    /// A stand-in for an algorithm a host registers via <c>AddPasswordHashAlgorithm&lt;T&gt;()</c> to keep
    /// verifying (and rehashing) passwords it hashed before switching to the default PBKDF2-SHA512 algorithm.
    /// Kept outside the how-to's snippets: the registration call itself is shown in
    /// IdentityServer8.UnitTests.PasswordsHowToTests.
    /// </summary>
    private sealed class FixtureLegacyPasswordHashAlgorithm : IPasswordHashAlgorithm
    {
        public const string AlgorithmId = "fixture-legacy-algo";

        string IPasswordHashAlgorithm.AlgorithmId => AlgorithmId;

        public HashedPasswordData Hash(string password) =>
            new(AlgorithmId, System.Text.Encoding.UTF8.GetBytes(password), [], new Dictionary<string, string>());

        public bool Verify(string password, HashedPasswordData data) =>
            System.Text.Encoding.UTF8.GetBytes(password).SequenceEqual([.. data.Hash]);

        public bool NeedsRehash(HashedPasswordData data) => data.AlgorithmId != AlgorithmId;
    }
}
