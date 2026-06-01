// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.RecoveryCodes;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class AuthenticationAttemptThrottling : IAsyncLifetime
{
    private IPasswordAuthenticator _passwordAuthenticator = null!;
    private IRecoveryCodeAuthenticator _recoveryCodeAuthenticator = null!;
    private ITotpAuthenticator _totpAuthenticator = null!;
    private IUserAuthenticatorsSelfService _authenticatorsSelfService = null!;
    private IExternalAuthenticator _externalAuthenticator = null!;
    private IUserProfileSelfService _profileSelfService = null!;
    private IUserProfileSchemaAdmin _schemaAdmin = null!;
    private IUserSelfService _userSelfService = null!;
    private ServiceProvider _serviceProvider = null!;
    private FakeTimeProvider _timeProvider = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Throttling.MaxFailedAttempts = 2;
            options.Throttling.ThrottleDuration = TimeSpan.FromMinutes(5);
            options.Throttling.FailureWindow = TimeSpan.FromMinutes(15);
        });
        _passwordAuthenticator = _serviceProvider.GetRequiredService<IPasswordAuthenticator>();
        _recoveryCodeAuthenticator = _serviceProvider.GetRequiredService<IRecoveryCodeAuthenticator>();
        _totpAuthenticator = _serviceProvider.GetRequiredService<ITotpAuthenticator>();
        _authenticatorsSelfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _externalAuthenticator = _serviceProvider.GetRequiredService<IExternalAuthenticator>();
        _profileSelfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
        _schemaAdmin = _serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        _userSelfService = _serviceProvider.GetRequiredService<IUserSelfService>();
        _timeProvider = _serviceProvider.GetRequiredService<FakeTimeProvider>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Password_authentication_rejects_after_threshold_and_allows_after_throttle_duration()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var (correctPassword, correctSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);
        var (_, wrongSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, ct: _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();
        (await _authenticatorsSelfService.TrySetPasswordAsync(subjectId, correctPassword, _ct)).ShouldBeTrue();

        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, wrongSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, wrongSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        _timeProvider.SetUtcNow(_timeProvider.GetUtcNow().AddMinutes(6));

        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Successful_password_authentication_resets_failure_count()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var (correctPassword, correctSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);
        var (_, wrongSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, ct: _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();
        (await _authenticatorsSelfService.TrySetPasswordAsync(subjectId, correctPassword, _ct)).ShouldBeTrue();

        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, wrongSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, wrongSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task First_successful_password_authentication_does_not_get_blocked_by_policy_context_creation()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var (correctPassword, correctSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();
        (await _authenticatorsSelfService.TrySetPasswordAsync(subjectId, correctPassword, _ct)).ShouldBeTrue();

        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Password_authentication_starts_a_fresh_window_after_failure_window_expires()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var (correctPassword, correctSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);
        var (_, wrongSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, ct: _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();
        (await _authenticatorsSelfService.TrySetPasswordAsync(subjectId, correctPassword, _ct)).ShouldBeTrue();

        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, wrongSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, wrongSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        _timeProvider.SetUtcNow(_timeProvider.GetUtcNow().AddMinutes(16));

        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, wrongSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Incorrect_old_password_in_change_password_counts_toward_password_throttling()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var (originalPassword, originalSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, subjectId, _ct);
        var (_, wrongSupplied) = await TestData.CreatePasswordPairAsync(_authenticatorsSelfService, ct: _ct);
        var newPassword = await TestData.CreatePasswordAsync(_authenticatorsSelfService, subjectId, _ct);

        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();
        (await _authenticatorsSelfService.TrySetPasswordAsync(subjectId, originalPassword, _ct)).ShouldBeTrue();

        (await _authenticatorsSelfService.TryChangePasswordAsync(subjectId, wrongSupplied, newPassword, _ct)).ShouldBeFalse();
        (await _authenticatorsSelfService.TryChangePasswordAsync(subjectId, wrongSupplied, newPassword, _ct)).ShouldBeFalse();

        (await _authenticatorsSelfService.TryChangePasswordAsync(subjectId, originalSupplied, newPassword, _ct)).ShouldBeFalse();
        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, originalSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        _timeProvider.SetUtcNow(_timeProvider.GetUtcNow().AddMinutes(6));

        _ = (await _passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, originalSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Totp_authentication_rejects_after_threshold()
    {
        var subjectId = await _authenticatorsSelfService.CreateUserWithTotpAuthenticator(
            _externalAuthenticator,
            TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000),
            _timeProvider,
            _ct);

        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        (await _totpAuthenticator.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, PlainTextTotp.Create("123456"), _ct)).ShouldBeFalse();
        (await _totpAuthenticator.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, PlainTextTotp.Create("123456"), _ct)).ShouldBeFalse();
        (await _totpAuthenticator.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, PlainTextTotp.Create(TestData.Totp2005), _ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Recovery_code_authentication_rejects_after_threshold_and_allows_after_throttle_duration()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var codes = (await _authenticatorsSelfService.TryCreateRecoveryCodesAsync(subjectId, _ct)).ShouldNotBeNull();

        PlainTextRecoveryCode.TryCreate("123456-abcdefg", out var wrongCode).ShouldBeTrue();

        (await _recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, wrongCode!.Value, _ct)).ShouldBeFalse();
        (await _recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, wrongCode.Value, _ct)).ShouldBeFalse();
        (await _recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, codes.ElementAt(0), _ct)).ShouldBeFalse();

        _timeProvider.SetUtcNow(_timeProvider.GetUtcNow().AddMinutes(6));

        (await _recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, codes.ElementAt(0), _ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Concurrent_failed_password_authentication_attempts_are_both_counted_when_the_first_write_conflicts()
    {
        await using var serviceProvider = await CreateServiceProviderWithConcurrentBarrierPolicy();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passwordAuthenticator = serviceProvider.GetRequiredService<IPasswordAuthenticator>();
        var passwordFactory = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var profileSelfService = serviceProvider.GetRequiredService<IUserProfileSelfService>();
        var schemaAdmin = serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        var subjectId = await serviceProvider.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var (correctPassword, correctSupplied) = await TestData.CreatePasswordPairAsync(passwordFactory, subjectId, _ct);
        var (_, wrongSupplied) = await TestData.CreatePasswordPairAsync(passwordFactory, ct: _ct);

        await TestData.AddAttributeDefinitions(schemaAdmin, _ct);
        var schema = await profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();
        (await selfService.TrySetPasswordAsync(subjectId, correctPassword, _ct)).ShouldBeTrue();

        var attempts = await Task.WhenAll(
            Task.Run(() => passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, wrongSupplied, _ct), _ct),
            Task.Run(() => passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, wrongSupplied, _ct), _ct));

        attempts.ShouldAllBe(result => result is PasswordAuthenticationResult.Failure);
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    [Fact]
    public async Task Concurrent_failed_recovery_code_attempts_are_both_counted_when_the_first_write_conflicts()
    {
        await using var serviceProvider = await CreateServiceProviderWithConcurrentBarrierPolicy();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var recoveryCodeAuthenticator = serviceProvider.GetRequiredService<IRecoveryCodeAuthenticator>();
        var subjectId = await serviceProvider.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var codes = (await selfService.TryCreateRecoveryCodesAsync(subjectId, _ct)).ShouldNotBeNull();

        PlainTextRecoveryCode.TryCreate("123456-abcdefg", out var wrongCode).ShouldBeTrue();

        var attempts = await Task.WhenAll(
            Task.Run(() => recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, wrongCode!.Value, _ct), _ct),
            Task.Run(() => recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, wrongCode.Value, _ct), _ct));

        attempts.ShouldAllBe(result => result == false);
        (await recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, codes.ElementAt(0), _ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Concurrent_failed_totp_attempts_are_both_counted_when_the_first_write_conflicts()
    {
        await using var serviceProvider = await CreateServiceProviderWithConcurrentBarrierPolicy();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var totpAuthenticator = serviceProvider.GetRequiredService<ITotpAuthenticator>();
        var timeProvider = serviceProvider.GetRequiredService<FakeTimeProvider>();
        var subjectId = await selfService.CreateUserWithTotpAuthenticator(
            serviceProvider.GetRequiredService<IExternalAuthenticator>(),
            TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000),
            timeProvider,
            _ct);

        timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        var attempts = await Task.WhenAll(
            Task.Run(() => totpAuthenticator.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, PlainTextTotp.Create("123456"), _ct), _ct),
            Task.Run(() => totpAuthenticator.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, PlainTextTotp.Create("123456"), _ct), _ct));

        attempts.ShouldAllBe(result => result == false);
        (await totpAuthenticator.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, PlainTextTotp.Create(TestData.Totp2005), _ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Concurrent_failed_change_password_attempts_are_both_counted_when_the_first_write_conflicts()
    {
        await using var serviceProvider = await CreateServiceProviderWithConcurrentBarrierPolicy();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passwordAuthenticator = serviceProvider.GetRequiredService<IPasswordAuthenticator>();
        var passwordFactory = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var profileSelfService = serviceProvider.GetRequiredService<IUserProfileSelfService>();
        var schemaAdmin = serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        var subjectId = await serviceProvider.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var (correctPassword, correctSupplied) = await TestData.CreatePasswordPairAsync(passwordFactory, subjectId, _ct);
        var (_, wrongSupplied) = await TestData.CreatePasswordPairAsync(passwordFactory, ct: _ct);
        var newPassword = await TestData.CreatePasswordAsync(passwordFactory, subjectId, _ct);

        await TestData.AddAttributeDefinitions(schemaAdmin, _ct);
        var schema = await profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();
        (await selfService.TrySetPasswordAsync(subjectId, correctPassword, _ct)).ShouldBeTrue();

        var attempts = await Task.WhenAll(
            Task.Run(() => selfService.TryChangePasswordAsync(subjectId, wrongSupplied, newPassword, _ct), _ct),
            Task.Run(() => selfService.TryChangePasswordAsync(subjectId, wrongSupplied, newPassword, _ct), _ct));

        attempts.ShouldAllBe(result => result == false);
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    [Fact]
    public async Task Custom_authentication_attempt_policy_can_replace_the_default_behavior()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: _ => { },
            addDataProtection: false,
            configureServices: services => _ = services.Replace(ServiceDescriptor.Transient<IAuthenticationAttemptPolicy, AlwaysRejectAuthenticationAttemptPolicy>()));

        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passwordAuthenticator = serviceProvider.GetRequiredService<IPasswordAuthenticator>();
        var passwordFactory = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var profileSelfService = serviceProvider.GetRequiredService<IUserProfileSelfService>();
        var schemaAdmin = serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        var ct = TestContext.Current.CancellationToken;
        var subjectId = await serviceProvider.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), ct);
        var (correctPassword, correctSupplied) = await TestData.CreatePasswordPairAsync(passwordFactory, subjectId, ct);

        await TestData.AddAttributeDefinitions(schemaAdmin, ct);
        var schema = await profileSelfService.GetSchemaAsync(ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), ct)).ShouldNotBeNull();
        (await selfService.TrySetPasswordAsync(subjectId, correctPassword, ct)).ShouldBeTrue();

        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
    }

    // --- Velocity throttling tests ---

    [Fact]
    public async Task Password_authentication_rejects_after_velocity_threshold_even_with_correct_credentials()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Throttling.MaxAttemptsPerWindow = 3;
            options.Throttling.VelocityWindow = TimeSpan.FromSeconds(10);
            options.Throttling.VelocityThrottleDuration = TimeSpan.FromSeconds(30);
            // Set failure threshold high so only velocity fires
            options.Throttling.MaxFailedAttempts = 100;
        });
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passwordAuthenticator = serviceProvider.GetRequiredService<IPasswordAuthenticator>();
        var passwordFactory = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var profileSelfService = serviceProvider.GetRequiredService<IUserProfileSelfService>();
        var schemaAdmin = serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        var timeProvider = serviceProvider.GetRequiredService<FakeTimeProvider>();
        var ct = TestContext.Current.CancellationToken;

        var subjectId = await serviceProvider.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), ct);
        var (correctPassword, correctSupplied) = await TestData.CreatePasswordPairAsync(passwordFactory, subjectId, ct);

        await TestData.AddAttributeDefinitions(schemaAdmin, ct);
        var schema = await profileSelfService.GetSchemaAsync(ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), ct)).ShouldNotBeNull();
        (await selfService.TrySetPasswordAsync(subjectId, correctPassword, ct)).ShouldBeTrue();

        // 3 successful attempts — hits velocity threshold
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();

        // 4th attempt within window — should be rejected even with correct credentials
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // After velocity throttle duration, should be allowed again
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddSeconds(31));
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Totp_authentication_rejects_after_velocity_threshold()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Throttling.MaxAttemptsPerWindow = 3;
            options.Throttling.VelocityWindow = TimeSpan.FromSeconds(10);
            options.Throttling.VelocityThrottleDuration = TimeSpan.FromSeconds(30);
            options.Throttling.MaxFailedAttempts = 100;
        });
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var totpAuthenticator = serviceProvider.GetRequiredService<ITotpAuthenticator>();
        var timeProvider = serviceProvider.GetRequiredService<FakeTimeProvider>();
        var ct = TestContext.Current.CancellationToken;

        var subjectId = await selfService.CreateUserWithTotpAuthenticator(
            serviceProvider.GetRequiredService<IExternalAuthenticator>(),
            TestData.UnixTimeSeconds2000,
            PlainTextTotp.Create(TestData.Totp2000),
            timeProvider,
            ct);

        timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2000));

        // 3 failed attempts — hits velocity threshold
        (await totpAuthenticator.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, PlainTextTotp.Create("000000"), ct)).ShouldBeFalse();
        (await totpAuthenticator.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, PlainTextTotp.Create("000000"), ct)).ShouldBeFalse();
        (await totpAuthenticator.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, PlainTextTotp.Create("000000"), ct)).ShouldBeFalse();

        // 4th attempt — rejected by velocity throttle
        (await totpAuthenticator.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, PlainTextTotp.Create(TestData.Totp2000), ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Recovery_code_authentication_rejects_after_velocity_threshold()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Throttling.MaxAttemptsPerWindow = 3;
            options.Throttling.VelocityWindow = TimeSpan.FromSeconds(10);
            options.Throttling.VelocityThrottleDuration = TimeSpan.FromSeconds(30);
            options.Throttling.MaxFailedAttempts = 100;
        });
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var recoveryCodeAuthenticator = serviceProvider.GetRequiredService<IRecoveryCodeAuthenticator>();
        var timeProvider = serviceProvider.GetRequiredService<FakeTimeProvider>();
        var ct = TestContext.Current.CancellationToken;

        var subjectId = await serviceProvider.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), ct);
        var codes = (await selfService.TryCreateRecoveryCodesAsync(subjectId, ct)).ShouldNotBeNull();

        PlainTextRecoveryCode.TryCreate("123456-abcdefg", out var wrongCode).ShouldBeTrue();

        // 3 failed attempts — hits velocity threshold
        (await recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, wrongCode!.Value, ct)).ShouldBeFalse();
        (await recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, wrongCode.Value, ct)).ShouldBeFalse();
        (await recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, wrongCode.Value, ct)).ShouldBeFalse();

        // 4th attempt with valid code — rejected by velocity throttle
        (await recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, codes.ElementAt(0), ct)).ShouldBeFalse();

        // After velocity throttle duration, valid code works
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddSeconds(31));
        (await recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, codes.ElementAt(0), ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Velocity_throttle_is_per_authenticator_not_per_user()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Throttling.MaxAttemptsPerWindow = 3;
            options.Throttling.VelocityWindow = TimeSpan.FromSeconds(10);
            options.Throttling.VelocityThrottleDuration = TimeSpan.FromSeconds(30);
            options.Throttling.MaxFailedAttempts = 100;
        });

        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passwordAuthenticator = serviceProvider.GetRequiredService<IPasswordAuthenticator>();
        var recoveryCodeAuthenticator = serviceProvider.GetRequiredService<IRecoveryCodeAuthenticator>();
        var passwordFactory = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var profileSelfService = serviceProvider.GetRequiredService<IUserProfileSelfService>();
        var schemaAdmin = serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        var ct = TestContext.Current.CancellationToken;

        var subjectId = await serviceProvider.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), ct);
        var (correctPassword, correctSupplied) = await TestData.CreatePasswordPairAsync(passwordFactory, subjectId, ct);
        var codes = (await selfService.TryCreateRecoveryCodesAsync(subjectId, ct)).ShouldNotBeNull();

        await TestData.AddAttributeDefinitions(schemaAdmin, ct);
        var schema = await profileSelfService.GetSchemaAsync(ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), ct)).ShouldNotBeNull();
        (await selfService.TrySetPasswordAsync(subjectId, correctPassword, ct)).ShouldBeTrue();

        // Exhaust velocity for password authenticator
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>(); // velocity blocked

        // Recovery code authenticator should still work (different authenticator, separate velocity counter)
        (await recoveryCodeAuthenticator.TryAuthenticateAsync(subjectId, codes.ElementAt(0), ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Velocity_window_expiry_allows_attempts_again()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Throttling.MaxAttemptsPerWindow = 3;
            options.Throttling.VelocityWindow = TimeSpan.FromSeconds(10);
            options.Throttling.VelocityThrottleDuration = TimeSpan.FromSeconds(30);
            options.Throttling.MaxFailedAttempts = 100;
        });
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passwordAuthenticator = serviceProvider.GetRequiredService<IPasswordAuthenticator>();
        var passwordFactory = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var profileSelfService = serviceProvider.GetRequiredService<IUserProfileSelfService>();
        var schemaAdmin = serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        var timeProvider = serviceProvider.GetRequiredService<FakeTimeProvider>();
        var ct = TestContext.Current.CancellationToken;

        var subjectId = await serviceProvider.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), ct);
        var (correctPassword, correctSupplied) = await TestData.CreatePasswordPairAsync(passwordFactory, subjectId, ct);

        await TestData.AddAttributeDefinitions(schemaAdmin, ct);
        var schema = await profileSelfService.GetSchemaAsync(ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), ct)).ShouldNotBeNull();
        (await selfService.TrySetPasswordAsync(subjectId, correctPassword, ct)).ShouldBeTrue();

        // 3 attempts — hits velocity threshold
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();

        // Advance time past both velocity window AND throttle duration
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddSeconds(31));

        // Timestamps are now outside the velocity window — should be allowed
        _ = (await passwordAuthenticator.TryAuthenticateAsync(attribute.Code, attribute.UntypedValue, correctSupplied, ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    private sealed class AlwaysRejectAuthenticationAttemptPolicy : IAuthenticationAttemptPolicy
    {
        public Task<AuthenticationAttemptDecision> EvaluateAsync(
            AuthenticationAttemptContext context,
            CancellationToken ct) =>
            Task.FromResult<AuthenticationAttemptDecision>(new AuthenticationAttemptDecision.Reject());
    }

    private static async Task<ServiceProvider> CreateServiceProviderWithConcurrentBarrierPolicy() =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: options =>
            {
                options.Throttling.MaxFailedAttempts = 2;
                options.Throttling.ThrottleDuration = TimeSpan.FromMinutes(5);
                options.Throttling.FailureWindow = TimeSpan.FromMinutes(15);
            },
            addDataProtection: false,
            configureServices: services => _ = services.Replace(ServiceDescriptor.Singleton<IAuthenticationAttemptPolicy, WaitForConcurrentAttemptsAuthenticationAttemptPolicy>()));

    private sealed class WaitForConcurrentAttemptsAuthenticationAttemptPolicy(
        IOptions<UserAuthenticationOptions> options,
        TimeProvider timeProvider) : IAuthenticationAttemptPolicy
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attemptCount;

        public async Task<AuthenticationAttemptDecision> EvaluateAsync(
            AuthenticationAttemptContext context,
            CancellationToken ct)
        {
            if (Interlocked.Increment(ref _attemptCount) == 2)
            {
                _ = _release.TrySetResult();
            }

            await _release.Task.WaitAsync(ct);

            var throttling = options.Value.Throttling;
            var attemptInfo = context.AttemptInfo;
            var now = timeProvider.GetUtcNow();

            if (attemptInfo.LastFailedAtUtc is not null
                && now >= attemptInfo.LastFailedAtUtc.Value + throttling.FailureWindow)
            {
                return new AuthenticationAttemptDecision.Allow();
            }

            if (attemptInfo.FailedAttemptCount < throttling.MaxFailedAttempts)
            {
                return new AuthenticationAttemptDecision.Allow();
            }

            if (attemptInfo.LastFailedAtUtc is null)
            {
                return new AuthenticationAttemptDecision.Reject();
            }

            var blockedUntil = attemptInfo.LastFailedAtUtc.Value + throttling.ThrottleDuration;
            return now >= blockedUntil
                ? new AuthenticationAttemptDecision.Allow()
                : new AuthenticationAttemptDecision.Reject();
        }
    }
}
