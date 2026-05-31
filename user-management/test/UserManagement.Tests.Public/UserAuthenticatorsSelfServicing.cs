// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class UserAuthenticatorsSelfServicing : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IPasswordAuth _passwordAuth = null!;
    private IUserAuthenticatorsSelfService _authenticatorsSelfService = null!;
    private ServiceProvider _serviceProvider = null!;
    private FakeTimeProvider _timeProvider = null!;
    private IUserProfileSelfService _profileSelfService = null!;
    private IUserProfileSchemaAdmin _schemaAdmin = null!;
    private FakeOtpDispatcher _otpDispatcher = null!;
    private IOtpSender _otpSender = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _passwordAuth = _serviceProvider.GetRequiredService<IPasswordAuth>();
        _authenticatorsSelfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _timeProvider = _serviceProvider.GetRequiredService<FakeTimeProvider>();
        _profileSelfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
        _schemaAdmin = _serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        _otpDispatcher = _serviceProvider.GetRequiredService<FakeOtpDispatcher>();
        _otpSender = _serviceProvider.GetRequiredService<IOtpSender>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Can_register_with_ExternalAuthenticator()
    {
        var authenticator = TestData.CreateExternalAuthenticator();

        var user = await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), authenticator, _ct);

        user.ShouldNotBeNull().ExternalAuthenticators.ShouldHaveSingleItem().ShouldBe(authenticator);
    }

    [Fact]
    public async Task Cannot_register_with_ExternalAuthenticator_if_exists()
    {
        var authenticator = TestData.CreateExternalAuthenticator();
        _ = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), authenticator, _ct)).ShouldNotBeNull();

        var user = await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), authenticator, _ct);

        user.ShouldBeNull();
    }

    [Fact]
    public async Task Can_get_by_SubjectId()
    {
        var expected = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        var actual = await _authenticatorsSelfService.TryGetAsync(expected.SubjectId, _ct);

        actual.ShouldNotBeNull().SubjectId.ShouldBe(expected.SubjectId);
    }

    [Fact]
    public async Task Can_get_by_ExternalAuthenticator()
    {
        var authenticator = TestData.CreateExternalAuthenticator();
        var expected = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), authenticator, _ct)).ShouldNotBeNull();

        var actual = await _authenticatorsSelfService.TryGetAsync(authenticator, _ct);

        actual.ShouldNotBeNull().SubjectId.ShouldBe(expected.SubjectId);
    }

    [Fact]
    public async Task Can_add_OtpAddress()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var address = TestData.CreateOtpAddress();
        var (otp, token) = await SendOtpAsync(address);

        var added = await _authenticatorsSelfService.TryAddOtpAddressAsync(user.SubjectId, otp, token, _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull()
            .OtpAddresses.ShouldBe([address]);
    }

    [Fact]
    public async Task Cannot_add_OtpAddress_with_incorrect_otp()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var address = TestData.CreateOtpAddress();
        var (_, token) = await SendOtpAsync(address);
        var wrongOtp = PlainTextOtp.Create("00000000");

        var added = await _authenticatorsSelfService.TryAddOtpAddressAsync(user.SubjectId, wrongOtp, token, _ct);

        added.ShouldBeFalse();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull()
            .OtpAddresses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cannot_add_OtpAddress_with_incorrect_token()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var address = TestData.CreateOtpAddress();
        var (otp, _) = await SendOtpAsync(address);
        var wrongToken = OtpToken.New();

        var added = await _authenticatorsSelfService.TryAddOtpAddressAsync(user.SubjectId, otp, wrongToken, _ct);

        added.ShouldBeFalse();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull()
            .OtpAddresses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_add_OtpAddressIfExists()
    {
        var address = TestData.CreateOtpAddress();
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var (otp1, token1) = await SendOtpAsync(address);
        (await _authenticatorsSelfService.TryAddOtpAddressAsync(user.SubjectId, otp1, token1, _ct)).ShouldBeTrue();

        var (otp2, token2) = await SendOtpAsync(address);
        var added = await _authenticatorsSelfService.TryAddOtpAddressAsync(user.SubjectId, otp2, token2, _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().OtpAddresses.ShouldBe([address]);
    }

    [Fact]
    public async Task Can_remove_OtpAddress()
    {
        var address = TestData.CreateOtpAddress();
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var (otp, token) = await SendOtpAsync(address);
        (await _authenticatorsSelfService.TryAddOtpAddressAsync(user.SubjectId, otp, token, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsSelfService.TryRemoveOtpAddressAsync(user.SubjectId, address, _ct);

        removed.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().OtpAddresses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_remove_OtpAddressIfNotExists()
    {
        var address = TestData.CreateOtpAddress();
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var (otp, token) = await SendOtpAsync(address);
        (await _authenticatorsSelfService.TryAddOtpAddressAsync(user.SubjectId, otp, token, _ct)).ShouldBeTrue();
        (await _authenticatorsSelfService.TryRemoveOtpAddressAsync(user.SubjectId, address, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsSelfService.TryRemoveOtpAddressAsync(user.SubjectId, address, _ct);

        removed.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_add_ExternalAuthenticator()
    {
        var authenticator1 = TestData.CreateExternalAuthenticator();
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), authenticator1, _ct)).ShouldNotBeNull();
        var authenticator2 = TestData.CreateExternalAuthenticator();

        var added = await _authenticatorsSelfService.TryAddExternalAuthenticatorAsync(user.SubjectId, authenticator2, _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().ExternalAuthenticators
            .ShouldBe([authenticator1, authenticator2]);
    }

    [Fact]
    public async Task Can_add_ExternalAuthenticatorIfExists()
    {
        var authenticator = TestData.CreateExternalAuthenticator();
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), authenticator, _ct)).ShouldNotBeNull();

        var added = await _authenticatorsSelfService.TryAddExternalAuthenticatorAsync(user.SubjectId, authenticator, _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull()
            .ExternalAuthenticators.ShouldHaveSingleItem().ShouldBe(authenticator);
    }

    [Fact]
    public async Task Can_remove_ExternalAuthenticator()
    {
        var authenticator = TestData.CreateExternalAuthenticator();
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), authenticator, _ct)).ShouldNotBeNull();

        var removed = await _authenticatorsSelfService.TryRemoveExternalAuthenticatorAsync(user.SubjectId, authenticator, _ct);

        removed.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().ExternalAuthenticators.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_remove_ExternalAuthenticatorIfNotExists()
    {
        var authenticator = TestData.CreateExternalAuthenticator();
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), authenticator, _ct)).ShouldNotBeNull();
        (await _authenticatorsSelfService.TryRemoveExternalAuthenticatorAsync(user.SubjectId, authenticator, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsSelfService.TryRemoveExternalAuthenticatorAsync(user.SubjectId, authenticator, _ct);

        removed.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_add_TotpAuthenticator()
    {
        var subjectId = await _authenticatorsSelfService.CreateUserWithTotpAuthenticator(TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        var name1 = (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpAuthenticatorNames
            .ShouldHaveSingleItem();
        var name2 = TotpAuthenticatorName.Create($"{nameof(TotpAuthenticatorName)}2");
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        var added = await _authenticatorsSelfService.TryAddTotpAuthenticatorAsync(subjectId, name2, TestData.TotpKey,
            PlainTextTotp.Create(TestData.Totp2005), _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpAuthenticatorNames
            .ShouldBe([name1, name2]);
    }

    [Fact]
    public async Task Cannot_add_TotpAuthenticator_if_exists()
    {
        var subjectId = await _authenticatorsSelfService.CreateUserWithTotpAuthenticator(TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        var name = (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpAuthenticatorNames
            .ShouldHaveSingleItem();
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        var added = await _authenticatorsSelfService.TryAddTotpAuthenticatorAsync(subjectId, name, TestData.TotpKey,
            PlainTextTotp.Create(TestData.Totp2005), _ct);

        added.ShouldBeFalse();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpAuthenticatorNames.ShouldBe([name]);
    }

    [Fact]
    public async Task Can_remove_TotpAuthenticator()
    {
        var subjectId = await _authenticatorsSelfService.CreateUserWithTotpAuthenticator(TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        var name = (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpAuthenticatorNames
            .ShouldHaveSingleItem();

        var removed = await _authenticatorsSelfService.TryRemoveTotpAuthenticatorAsync(subjectId, name, _ct);

        removed.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpAuthenticatorNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_remove_TotpAuthenticatorIfNotExists()
    {
        var subjectId = await _authenticatorsSelfService.CreateUserWithTotpAuthenticator(TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        var name = (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpAuthenticatorNames
            .ShouldHaveSingleItem();
        (await _authenticatorsSelfService.TryRemoveTotpAuthenticatorAsync(subjectId, name, _ct)).ShouldBe(true);
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpAuthenticatorNames.ShouldBeEmpty();

        var removed = await _authenticatorsSelfService.TryRemoveTotpAuthenticatorAsync(subjectId, name, _ct);

        removed.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_add_Passkey()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var passkey = TestData.CreatePasskeyCredential("Test Passkey");

        var added = await _authenticatorsSelfService.TryAddPasskeyAsync(user.SubjectId, passkey, _ct);

        added.ShouldBeTrue();
        _ = (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().Passkeys.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Can_remove_Passkey()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var passkey = TestData.CreatePasskeyCredential("Test Passkey");
        (await _authenticatorsSelfService.TryAddPasskeyAsync(user.SubjectId, passkey, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsSelfService.TryRemovePasskeyAsync(user.SubjectId, passkey.CredentialId, _ct);

        removed.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().Passkeys.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cannot_remove_Passkey_if_not_exists()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var passkey = TestData.CreatePasskeyCredential("Test Passkey");
        (await _authenticatorsSelfService.TryAddPasskeyAsync(user.SubjectId, passkey, _ct)).ShouldBeTrue();
        (await _authenticatorsSelfService.TryRemovePasskeyAsync(user.SubjectId, passkey.CredentialId, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsSelfService.TryRemovePasskeyAsync(user.SubjectId, passkey.CredentialId, _ct);

        removed.ShouldBeFalse();
    }

    [Fact]
    public async Task Cannot_add_Passkey_with_duplicate_name()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var passkey1 = TestData.CreatePasskeyCredential("My Passkey");
        (await _authenticatorsSelfService.TryAddPasskeyAsync(user.SubjectId, passkey1, _ct)).ShouldBeTrue();
        var passkey2 = TestData.CreatePasskeyCredential("My Passkey");

        var added = await _authenticatorsSelfService.TryAddPasskeyAsync(user.SubjectId, passkey2, _ct);

        added.ShouldBeFalse();
        _ = (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().Passkeys.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Can_add_Passkeys_with_distinct_names()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var passkey1 = TestData.CreatePasskeyCredential("First Passkey");
        (await _authenticatorsSelfService.TryAddPasskeyAsync(user.SubjectId, passkey1, _ct)).ShouldBeTrue();
        var passkey2 = TestData.CreatePasskeyCredential("Second Passkey");

        var added = await _authenticatorsSelfService.TryAddPasskeyAsync(user.SubjectId, passkey2, _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().Passkeys.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TotpKey_is_DataProtected()
    {
        var path = Guid.NewGuid();

        // arrange
        await using var serviceProvider1 = await CreateServiceProvider(path);
        var selfService1 = serviceProvider1.GetRequiredService<IUserAuthenticatorsSelfService>();
        var timeProvider1 = serviceProvider1.GetRequiredService<FakeTimeProvider>();
        var totpAuth1 = serviceProvider1.GetRequiredService<ITotpAuth>();
        var subjectId = await selfService1.CreateUserWithTotpAuthenticator(TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000), timeProvider1, _ct);
        var totpAuthenticatorName = (await selfService1.TryGetAsync(subjectId, _ct)).ShouldNotBeNull()
            .TotpAuthenticatorNames.ShouldHaveSingleItem();
        timeProvider1.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));
        (await totpAuth1.TryAuthenticateAsync(subjectId, TotpAuthenticatorName.Default,
            PlainTextTotp.Create(TestData.Totp2005), _ct)).ShouldBeTrue();

        await using var serviceProvider2 =
            await CreateServiceProvider(path,
                options => options.Totp.Storage.ProtectKeys = false);
        var selfService2 = serviceProvider2.GetRequiredService<IUserAuthenticatorsSelfService>();
        var timeProvider2 = serviceProvider2.GetRequiredService<FakeTimeProvider>();
        var totpAuth2 = serviceProvider2.GetRequiredService<ITotpAuth>();
        (await selfService2.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpAuthenticatorNames
            .ShouldBe([totpAuthenticatorName]);
        timeProvider2.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        // act
        var result = await totpAuth2.TryAuthenticateAsync(subjectId, TotpAuthenticatorName.Default,
            PlainTextTotp.Create(TestData.Totp2005), _ct);

        // assert
        result.ShouldBeFalse();

    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Can_create_RecoveryCodes()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        var codes = await _authenticatorsSelfService.TryCreateRecoveryCodesAsync(user.SubjectId, _ct);

        codes.ShouldNotBeNull().Count.ShouldBe(10);
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Can_set_password()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var (password, supplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, user.SubjectId, _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryRegisterAsync(user.SubjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var isSet = await _authenticatorsSelfService.TrySetPasswordAsync(user.SubjectId, password, _ct);

        isSet.ShouldBeTrue();
        _ = (await _passwordAuth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, supplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Cannot_set_password_if_has_password()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryRegisterAsync(user.SubjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var (originalPassword, originalSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, user.SubjectId, _ct);
        (await _authenticatorsSelfService.TrySetPasswordAsync(user.SubjectId, originalPassword, _ct)).ShouldBeTrue();
        var (newPassword, newSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, user.SubjectId, _ct);

        var isSet = await _authenticatorsSelfService.TrySetPasswordAsync(user.SubjectId, newPassword, _ct);

        isSet.ShouldBeFalse();
        _ = (await _passwordAuth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, originalSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await _passwordAuth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, newSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Can_change_password()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryRegisterAsync(user.SubjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var (originalPassword, originalSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, user.SubjectId, _ct);
        (await _authenticatorsSelfService.TrySetPasswordAsync(user.SubjectId, originalPassword, _ct)).ShouldBeTrue();
        var (newPassword, newSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, user.SubjectId, _ct);

        var changed = await _authenticatorsSelfService.TryChangePasswordAsync(user.SubjectId, originalSupplied, newPassword, _ct);

        changed.ShouldBeTrue();
        _ = (await _passwordAuth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, originalSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await _passwordAuth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, newSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Cannot_change_password_if_old_password_not_provided()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryRegisterAsync(user.SubjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var (originalPassword, originalSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, user.SubjectId, _ct);
        (await _authenticatorsSelfService.TrySetPasswordAsync(user.SubjectId, originalPassword, _ct)).ShouldBeTrue();
        var (_, otherSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, ct: _ct);
        var (newPassword, newSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, user.SubjectId, _ct);

        var changed = await _authenticatorsSelfService.TryChangePasswordAsync(user.SubjectId, otherSupplied, newPassword, _ct);

        changed.ShouldBeFalse();
        _ = (await _passwordAuth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, originalSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await _passwordAuth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, otherSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await _passwordAuth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, newSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Can_reset_password()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryRegisterAsync(user.SubjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var (originalPassword, originalSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, user.SubjectId, _ct);
        (await _authenticatorsSelfService.TrySetPasswordAsync(user.SubjectId, originalPassword, _ct)).ShouldBeTrue();
        var (newPassword, newSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, user.SubjectId, _ct);

        var isReset = await _authenticatorsSelfService.TryResetPasswordAsync(user.SubjectId, newPassword, _ct);

        isReset.ShouldBeTrue();
        _ = (await _passwordAuth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, originalSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await _passwordAuth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, newSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Cannot_reset_password_if_has_no_password()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryRegisterAsync(user.SubjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var (password, supplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, user.SubjectId, _ct);

        var isReset = await _authenticatorsSelfService.TryResetPasswordAsync(user.SubjectId, password, _ct);

        isReset.ShouldBeFalse();
        _ = (await _passwordAuth.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, supplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    private async Task<(PlainTextOtp Otp, OtpToken Token)> SendOtpAsync(OtpAddress address)
    {
        var callCountBefore = _otpDispatcher.Calls.Count;
        var result = (await _otpSender.TrySendOtpAsync(address, _ct)).ShouldNotBeNull();
        result.Sent.ShouldBeTrue();
        var otp = _otpDispatcher.Calls.ElementAt(callCountBefore).Otp;
        return (otp, result.Token.Value);
    }

    private static async Task<ServiceProvider> CreateServiceProvider(
        Guid dbId, Action<UserAuthenticationOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        _ = services
            .AddLogging()
            .AddSingleton(new FakeTimeProvider())
            .AddSingleton<TimeProvider>(provider => provider.GetRequiredService<FakeTimeProvider>())
            .AddSingleton(new FakeOtpDispatcher())
            .AddSingleton<IOtpDispatcher>(provider => provider.GetRequiredService<FakeOtpDispatcher>());

        _ = services.AddUserManagementInternal(users =>
        {
            // modules registered unconditionally by AddUserManagementInternal
            _ = users.AddSqliteStore(opt => opt.ConnectionString = $"Data Source=MySharedDb_{dbId};Mode=Memory;Cache=Shared");
        });

        _ = services.Configure<UserAuthenticationOptions>(options =>
        {
            options.Passkeys.ServerDomain = "example.com";
            options.Passkeys.AllowedOrigins = ["https://example.com"];
        });

        if (configureOptions != null)
        {
            _ = services.Configure(configureOptions);
        }

        _ = services.AddDataProtection();

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IPooledStore>().MigrateAsync(CancellationToken.None);
        return sp;
    }
}
