// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class UserAuthenticatorsSelfServicing : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IPasswordAuthenticator _passwordAuthenticator = null!;
    private IUserAuthenticatorsSelfService _authenticatorsSelfService = null!;
    private IExternalAuthenticator _externalAuthenticator = null!;
    private ServiceProvider _serviceProvider = null!;
    private FakeTimeProvider _timeProvider = null!;
    private IUserProfileSelfService _profileSelfService = null!;
    private IUserProfileSchemaAdmin _schemaAdmin = null!;
    private FakeOtpDispatcher _otpDispatcher = null!;
    private IOtpSender _otpSender = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _passwordAuthenticator = _serviceProvider.GetRequiredService<IPasswordAuthenticator>();
        _authenticatorsSelfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _externalAuthenticator = _serviceProvider.GetRequiredService<IExternalAuthenticator>();
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
        var authenticator = TestData.CreateExternalAuthenticatorAddress();

        var subjectId = await _externalAuthenticator.CreateUserAsync(authenticator, _ct);

        var user = (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull();
        user.ExternalAuthenticatorAddresses.ShouldHaveSingleItem().ShouldBe(authenticator);
    }

    [Fact]
    public async Task Cannot_register_with_ExternalAuthenticator_if_exists()
    {
        var authenticator = TestData.CreateExternalAuthenticatorAddress();
        _ = await _externalAuthenticator.CreateUserAsync(authenticator, _ct);

        var result = await _externalAuthenticator.TryAuthenticateAsync(authenticator, _ct);

        // Second call returns the same user (idempotent), not a failure
        _ = result.ShouldBeOfType<ExternalAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Can_get_by_SubjectId()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);

        var actual = await _authenticatorsSelfService.TryGetAsync(subjectId, _ct);

        actual.ShouldNotBeNull().SubjectId.ShouldBe(subjectId);
    }

    [Fact]
    public async Task Can_get_by_ExternalAuthenticator()
    {
        var authenticator = TestData.CreateExternalAuthenticatorAddress();
        var subjectId = await _externalAuthenticator.CreateUserAsync(authenticator, _ct);

        var actual = await _authenticatorsSelfService.TryGetAsync(subjectId, _ct);

        actual.ShouldNotBeNull().SubjectId.ShouldBe(subjectId);
    }

    [Fact]
    public async Task Can_add_OtpAddress()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var address = TestData.CreateOtpAddress();
        var (otp, token) = await SendOtpAsync(address);

        var added = await _authenticatorsSelfService.TryAddOtpAddressAsync(subjectId, otp, token, _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull()
            .OtpAddresses.ShouldBe([address]);
    }

    [Fact]
    public async Task Cannot_add_OtpAddress_with_incorrect_otp()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var address = TestData.CreateOtpAddress();
        var (_, token) = await SendOtpAsync(address);
        var wrongOtp = PlainTextOtp.Create("00000000");

        var added = await _authenticatorsSelfService.TryAddOtpAddressAsync(subjectId, wrongOtp, token, _ct);

        added.ShouldBeFalse();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull()
            .OtpAddresses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cannot_add_OtpAddress_with_incorrect_token()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var address = TestData.CreateOtpAddress();
        var (otp, _) = await SendOtpAsync(address);
        var wrongToken = OtpToken.New();

        var added = await _authenticatorsSelfService.TryAddOtpAddressAsync(subjectId, otp, wrongToken, _ct);

        added.ShouldBeFalse();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull()
            .OtpAddresses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_add_OtpAddressIfExists()
    {
        var address = TestData.CreateOtpAddress();
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var (otp1, token1) = await SendOtpAsync(address);
        (await _authenticatorsSelfService.TryAddOtpAddressAsync(subjectId, otp1, token1, _ct)).ShouldBeTrue();

        var (otp2, token2) = await SendOtpAsync(address);
        var added = await _authenticatorsSelfService.TryAddOtpAddressAsync(subjectId, otp2, token2, _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().OtpAddresses.ShouldBe([address]);
    }

    [Fact]
    public async Task Can_remove_OtpAddress()
    {
        var address = TestData.CreateOtpAddress();
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var (otp, token) = await SendOtpAsync(address);
        (await _authenticatorsSelfService.TryAddOtpAddressAsync(subjectId, otp, token, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsSelfService.TryRemoveOtpAddressAsync(subjectId, address, _ct);

        removed.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().OtpAddresses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_remove_OtpAddressIfNotExists()
    {
        var address = TestData.CreateOtpAddress();
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var (otp, token) = await SendOtpAsync(address);
        (await _authenticatorsSelfService.TryAddOtpAddressAsync(subjectId, otp, token, _ct)).ShouldBeTrue();
        (await _authenticatorsSelfService.TryRemoveOtpAddressAsync(subjectId, address, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsSelfService.TryRemoveOtpAddressAsync(subjectId, address, _ct);

        removed.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_add_ExternalAuthenticator()
    {
        var authenticator1 = TestData.CreateExternalAuthenticatorAddress();
        var subjectId = await _externalAuthenticator.CreateUserAsync(authenticator1, _ct);
        var authenticator2 = TestData.CreateExternalAuthenticatorAddress();

        var added = await _authenticatorsSelfService.TryAddExternalAuthenticatorAddressAsync(subjectId, authenticator2, _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().ExternalAuthenticatorAddresses
            .ShouldBe([authenticator1, authenticator2]);
    }

    [Fact]
    public async Task Can_add_ExternalAuthenticatorIfExists()
    {
        var authenticator = TestData.CreateExternalAuthenticatorAddress();
        var subjectId = await _externalAuthenticator.CreateUserAsync(authenticator, _ct);

        var added = await _authenticatorsSelfService.TryAddExternalAuthenticatorAddressAsync(subjectId, authenticator, _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull()
            .ExternalAuthenticatorAddresses.ShouldHaveSingleItem().ShouldBe(authenticator);
    }

    [Fact]
    public async Task Can_remove_ExternalAuthenticator()
    {
        var authenticator = TestData.CreateExternalAuthenticatorAddress();
        var subjectId = await _externalAuthenticator.CreateUserAsync(authenticator, _ct);

        var removed = await _authenticatorsSelfService.TryRemoveExternalAuthenticatorAddressAsync(subjectId, authenticator, _ct);

        removed.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().ExternalAuthenticatorAddresses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_remove_ExternalAuthenticatorIfNotExists()
    {
        var authenticator = TestData.CreateExternalAuthenticatorAddress();
        var subjectId = await _externalAuthenticator.CreateUserAsync(authenticator, _ct);
        (await _authenticatorsSelfService.TryRemoveExternalAuthenticatorAddressAsync(subjectId, authenticator, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsSelfService.TryRemoveExternalAuthenticatorAddressAsync(subjectId, authenticator, _ct);

        removed.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_add_TotpAuthenticator()
    {
        var subjectId = await _authenticatorsSelfService.CreateUserWithTotpAuthenticator(_externalAuthenticator, TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        var name1 = (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpDeviceNames
            .ShouldHaveSingleItem();
        var name2 = TotpDeviceName.Create($"{nameof(TotpDeviceName)}2");
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        var added = await _authenticatorsSelfService.TryAddTotpDeviceAsync(subjectId, name2, TestData.TotpKey,
            PlainTextTotp.Create(TestData.Totp2005), _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpDeviceNames
            .ShouldBe([name1, name2]);
    }

    [Fact]
    public async Task Cannot_add_TotpAuthenticator_if_exists()
    {
        var subjectId = await _authenticatorsSelfService.CreateUserWithTotpAuthenticator(_externalAuthenticator, TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        var name = (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpDeviceNames
            .ShouldHaveSingleItem();
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        var added = await _authenticatorsSelfService.TryAddTotpDeviceAsync(subjectId, name, TestData.TotpKey,
            PlainTextTotp.Create(TestData.Totp2005), _ct);

        added.ShouldBeFalse();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpDeviceNames.ShouldBe([name]);
    }

    [Fact]
    public async Task Can_remove_TotpAuthenticator()
    {
        var subjectId = await _authenticatorsSelfService.CreateUserWithTotpAuthenticator(_externalAuthenticator, TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        var name = (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpDeviceNames
            .ShouldHaveSingleItem();

        var removed = await _authenticatorsSelfService.TryRemoveTotpDeviceAsync(subjectId, name, _ct);

        removed.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpDeviceNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_remove_TotpAuthenticatorIfNotExists()
    {
        var subjectId = await _authenticatorsSelfService.CreateUserWithTotpAuthenticator(_externalAuthenticator, TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        var name = (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpDeviceNames
            .ShouldHaveSingleItem();
        (await _authenticatorsSelfService.TryRemoveTotpDeviceAsync(subjectId, name, _ct)).ShouldBe(true);
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpDeviceNames.ShouldBeEmpty();

        var removed = await _authenticatorsSelfService.TryRemoveTotpDeviceAsync(subjectId, name, _ct);

        removed.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_add_Passkey()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var passkey = TestData.CreatePasskeyCredential("Test Passkey");

        var added = await _authenticatorsSelfService.TryAddPasskeyAsync(subjectId, passkey, _ct);

        added.ShouldBeTrue();
        _ = (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().Passkeys.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Can_remove_Passkey()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var passkey = TestData.CreatePasskeyCredential("Test Passkey");
        (await _authenticatorsSelfService.TryAddPasskeyAsync(subjectId, passkey, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsSelfService.TryRemovePasskeyAsync(subjectId, passkey.CredentialId, _ct);

        removed.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().Passkeys.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cannot_remove_Passkey_if_not_exists()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var passkey = TestData.CreatePasskeyCredential("Test Passkey");
        (await _authenticatorsSelfService.TryAddPasskeyAsync(subjectId, passkey, _ct)).ShouldBeTrue();
        (await _authenticatorsSelfService.TryRemovePasskeyAsync(subjectId, passkey.CredentialId, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsSelfService.TryRemovePasskeyAsync(subjectId, passkey.CredentialId, _ct);

        removed.ShouldBeFalse();
    }

    [Fact]
    public async Task Cannot_add_Passkey_with_duplicate_name()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var passkey1 = TestData.CreatePasskeyCredential("My Passkey");
        (await _authenticatorsSelfService.TryAddPasskeyAsync(subjectId, passkey1, _ct)).ShouldBeTrue();
        var passkey2 = TestData.CreatePasskeyCredential("My Passkey");

        var added = await _authenticatorsSelfService.TryAddPasskeyAsync(subjectId, passkey2, _ct);

        added.ShouldBeFalse();
        _ = (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().Passkeys.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Can_add_Passkeys_with_distinct_names()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var passkey1 = TestData.CreatePasskeyCredential("First Passkey");
        (await _authenticatorsSelfService.TryAddPasskeyAsync(subjectId, passkey1, _ct)).ShouldBeTrue();
        var passkey2 = TestData.CreatePasskeyCredential("Second Passkey");

        var added = await _authenticatorsSelfService.TryAddPasskeyAsync(subjectId, passkey2, _ct);

        added.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().Passkeys.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TotpKey_is_DataProtected()
    {
        var path = Guid.NewGuid();

        // arrange
        await using var serviceProvider1 = await CreateServiceProvider(path);
        var selfService1 = serviceProvider1.GetRequiredService<IUserAuthenticatorsSelfService>();
        var resolver1 = serviceProvider1.GetRequiredService<IExternalAuthenticator>();
        var timeProvider1 = serviceProvider1.GetRequiredService<FakeTimeProvider>();
        var totpAuthenticator1 = serviceProvider1.GetRequiredService<ITotpAuthenticator>();
        var subjectId = await selfService1.CreateUserWithTotpAuthenticator(resolver1, TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000), timeProvider1, _ct);
        var totpDeviceName = (await selfService1.TryGetAsync(subjectId, _ct)).ShouldNotBeNull()
            .TotpDeviceNames.ShouldHaveSingleItem();
        timeProvider1.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));
        (await totpAuthenticator1.TryAuthenticateAsync(subjectId, TotpDeviceName.Default,
            PlainTextTotp.Create(TestData.Totp2005), _ct)).ShouldBeTrue();

        await using var serviceProvider2 =
            await CreateServiceProvider(path,
                options => options.Totp.Storage.ProtectKeys = false);
        var selfService2 = serviceProvider2.GetRequiredService<IUserAuthenticatorsSelfService>();
        var timeProvider2 = serviceProvider2.GetRequiredService<FakeTimeProvider>();
        var totpAuthenticator2 = serviceProvider2.GetRequiredService<ITotpAuthenticator>();
        (await selfService2.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().TotpDeviceNames
            .ShouldBe([totpDeviceName]);
        timeProvider2.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        // act
        var result = await totpAuthenticator2.TryAuthenticateAsync(subjectId, TotpDeviceName.Default,
            PlainTextTotp.Create(TestData.Totp2005), _ct);

        // assert
        result.ShouldBeFalse();

    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Can_create_RecoveryCodes()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);

        var codes = await _authenticatorsSelfService.TryCreateRecoveryCodesAsync(subjectId, _ct);

        codes.ShouldNotBeNull().Count.ShouldBe(10);
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Can_set_password()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var (password, supplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var isSet = await _authenticatorsSelfService.TrySetPasswordAsync(subjectId, password, _ct);

        isSet.ShouldBeTrue();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, supplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Cannot_set_password_if_has_password()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var (originalPassword, originalSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);
        (await _authenticatorsSelfService.TrySetPasswordAsync(subjectId, originalPassword, _ct)).ShouldBeTrue();
        var (newPassword, newSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);

        var isSet = await _authenticatorsSelfService.TrySetPasswordAsync(subjectId, newPassword, _ct);

        isSet.ShouldBeFalse();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, originalSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, newSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Can_change_password()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var (originalPassword, originalSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);
        (await _authenticatorsSelfService.TrySetPasswordAsync(subjectId, originalPassword, _ct)).ShouldBeTrue();
        var (newPassword, newSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);

        var changed = await _authenticatorsSelfService.TryChangePasswordAsync(subjectId, originalSupplied, newPassword, _ct);

        changed.ShouldBeTrue();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, originalSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, newSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Cannot_change_password_if_old_password_not_provided()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var (originalPassword, originalSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);
        (await _authenticatorsSelfService.TrySetPasswordAsync(subjectId, originalPassword, _ct)).ShouldBeTrue();
        var (_, otherSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, ct: _ct);
        var (newPassword, newSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);

        var changed = await _authenticatorsSelfService.TryChangePasswordAsync(subjectId, otherSupplied, newPassword, _ct);

        changed.ShouldBeFalse();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, originalSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, otherSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, newSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Can_reset_password()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var (originalPassword, originalSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);
        (await _authenticatorsSelfService.TrySetPasswordAsync(subjectId, originalPassword, _ct)).ShouldBeTrue();
        var (newPassword, newSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);

        var isReset = await _authenticatorsSelfService.TryResetPasswordAsync(subjectId, newPassword, _ct);

        isReset.ShouldBeTrue();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, originalSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, newSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    [Trait("PasswordHashing", "True")]
    public async Task Cannot_reset_password_if_has_no_password()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var (password, supplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);

        var isReset = await _authenticatorsSelfService.TryResetPasswordAsync(subjectId, password, _ct);

        isReset.ShouldBeFalse();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, supplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    private async Task<(PlainTextOtp Otp, OtpToken Token)> SendOtpAsync(OtpAddress address)
    {
        var callCountBefore = _otpDispatcher.Calls.Count;
        var result = (await _otpSender.TrySendOtpAsync(address, _ct)).ShouldBeOfType<SendOtpResult.Sent>();
        var otp = _otpDispatcher.Calls.ElementAt(callCountBefore).Otp;
        return (otp, result.Token);
    }

    private static async Task<ServiceProvider> CreateServiceProvider(
        Guid dbId, Action<UserAuthenticationOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        _ = services
            .AddLogging()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
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
