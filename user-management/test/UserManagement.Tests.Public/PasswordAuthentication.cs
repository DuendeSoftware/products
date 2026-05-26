// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Import;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class PasswordAuthentication : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IPasswordAuth _auth = null!;
    private IUserAuthenticatorsSelfService _authenticatorsSelfService = null!;
    private IUserSelfService _userSelfService = null!;
    private ServiceProvider _serviceProvider = null!;
    private IUserImporter _importer = null!;
    private IPasswordHashAlgorithm _hashAlgorithm = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _auth = _serviceProvider.GetRequiredService<IPasswordAuth>();
        _authenticatorsSelfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _userSelfService = _serviceProvider.GetRequiredService<IUserSelfService>();
        _importer = _serviceProvider.GetRequiredService<IUserImporter>();
        _hashAlgorithm = _serviceProvider.GetRequiredService<IPasswordHashAlgorithm>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Can_authenticate_with_correct_password()
    {
        var userName = TestData.CreateUserName();
        var authenticators = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var (password, supplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, authenticators.SubjectId, _ct);
        (await _userSelfService.TrySetUserNameAsync(authenticators.SubjectId, userName, _ct)).ShouldBeTrue();
        (await _authenticatorsSelfService.TrySetPasswordAsync(authenticators.SubjectId, password, _ct)).ShouldBe(true);

        var actual = await _auth.TryAuthenticateAsync(userName, supplied, _ct);

        actual.ShouldBeOfType<PasswordAuthenticationResult.Success>().UserSubjectId.ShouldBe(authenticators.SubjectId);
    }

    [Fact]
    public async Task Cannot_authenticate_with_incorrect_password()
    {
        var userName = TestData.CreateUserName();
        var authenticators = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var (firstPassword, _) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, authenticators.SubjectId, _ct);
        (await _userSelfService.TrySetUserNameAsync(authenticators.SubjectId, userName, _ct)).ShouldBeTrue();
        (await _authenticatorsSelfService.TrySetPasswordAsync(authenticators.SubjectId, firstPassword, _ct)).ShouldBe(true);
        var (_, secondSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, ct: _ct);

        var actual = await _auth.TryAuthenticateAsync(userName, secondSupplied, _ct);

        _ = actual.ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    [Fact]
    public async Task Can_authenticate_with_password_that_violates_current_policy()
    {
        // Arrange: import a user with a password that violates the current policy.
        // "weak" has no uppercase, no digits, no symbols, and is below MinLength — the
        // factory would reject it. Import bypasses validation and stores the hash directly.
        const string rawPassword = "weak";
        (await _authenticatorsSelfService.TryCreatePasswordAsync(UserSubjectId.New(), rawPassword, _ct) is PasswordCreationResult.Failed).ShouldBeTrue("password should violate the current policy");

        var subjectId = UserSubjectId.New();
        var userName = TestData.CreateUserName();
        var hashedData = _hashAlgorithm.Hash(rawPassword);

        var batch = await _importer.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                UserName = userName,
                Authenticators = new AuthenticatorImport
                {
                    Password = new PasswordImport(hashedData)
                }
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);

        // Act: authenticate using NonValidatedPassword, which applies only minimal validation
        var supplied = NonValidatedPassword.Create(rawPassword);
        var actual = await _auth.TryAuthenticateAsync(userName, supplied, _ct);

        // Assert: authentication succeeds despite the password violating the current policy
        actual.ShouldBeOfType<PasswordAuthenticationResult.Success>().UserSubjectId.ShouldBe(subjectId);
    }
}
