// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Import;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class PasswordAuthentication : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IPasswordAuth _auth = null!;
    private IUserAuthenticatorsSelfService _authenticatorsSelfService = null!;
    private IUserProfileSelfService _profileSelfService = null!;
    private IUserProfileSchemaAdmin _schemaAdmin = null!;
    private ServiceProvider _serviceProvider = null!;
    private IUserImporter _importer = null!;
    private IPasswordHashAlgorithm _hashAlgorithm = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _auth = _serviceProvider.GetRequiredService<IPasswordAuth>();
        _authenticatorsSelfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _profileSelfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
        _schemaAdmin = _serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        _importer = _serviceProvider.GetRequiredService<IUserImporter>();
        _hashAlgorithm = _serviceProvider.GetRequiredService<IPasswordHashAlgorithm>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Can_authenticate_with_correct_password()
    {
        var authenticators = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var (password, supplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, authenticators.SubjectId, _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryRegisterAsync(authenticators.SubjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        (await _authenticatorsSelfService.TrySetPasswordAsync(authenticators.SubjectId, password, _ct)).ShouldBe(true);

        var actual = await _auth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, supplied, _ct);

        actual.ShouldBeOfType<PasswordAuthenticationResult.Success>().UserSubjectId.ShouldBe(authenticators.SubjectId);
    }

    [Fact]
    public async Task Cannot_authenticate_with_incorrect_password()
    {
        var authenticators = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var (firstPassword, _) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, authenticators.SubjectId, _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryRegisterAsync(authenticators.SubjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        (await _authenticatorsSelfService.TrySetPasswordAsync(authenticators.SubjectId, firstPassword, _ct)).ShouldBe(true);
        var (_, secondSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, ct: _ct);

        var actual = await _auth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, secondSupplied, _ct);

        _ = actual.ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    [Fact]
    public async Task Can_authenticate_with_password_that_violates_current_policy()
    {
        // Arrange: import a user with a password that violates the current policy.
        // "weak" has no uppercase, no digits, no symbols, and is below MinLength — the
        // factory would reject it. Import bypasses validation and stores the hash directly.
        const string rawPassword = "weak";
        (await _authenticatorsSelfService.TryValidatePasswordAsync(UserSubjectId.New(), rawPassword, _ct) is PasswordCreationResult.Failed).ShouldBeTrue("password should violate the current policy");

        var subjectId = UserSubjectId.New();
        var hashedData = _hashAlgorithm.Hash(rawPassword);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);

        var batch = await _importer.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                ProfileAttributes = attributes.Validate(),
                Authenticators = new AuthenticatorImport
                {
                    Password = new PasswordImport(hashedData)
                }
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);

        // Act: authenticate using NonValidatedPassword, which applies only minimal validation
        var supplied = NonValidatedPassword.Create(rawPassword);
        var actual = await _auth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, supplied, _ct);

        // Assert: authentication succeeds despite the password violating the current policy
        actual.ShouldBeOfType<PasswordAuthenticationResult.Success>().UserSubjectId.ShouldBe(subjectId);
    }
}
