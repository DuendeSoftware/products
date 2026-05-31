// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class EscalatingLockoutTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static async Task<(ServiceProvider ServiceProvider, IPasswordAuth PasswordAuth, IUserAuthenticatorsSelfService SelfService, IUserProfileSelfService ProfileSelfService, IUserProfileSchemaAdmin SchemaAdmin, FakeTimeProvider TimeProvider)> CreateAsync(
        Action<UserAuthenticationOptions> configure)
    {
        var sp = await UsersServiceProviderFactory.CreateWithOptionsAsync(configure);
        return (
            sp,
            sp.GetRequiredService<IPasswordAuth>(),
            sp.GetRequiredService<IUserAuthenticatorsSelfService>(),
            sp.GetRequiredService<IUserProfileSelfService>(),
            sp.GetRequiredService<IUserProfileSchemaAdmin>(),
            sp.GetRequiredService<FakeTimeProvider>()
        );
    }

    private async Task<(AttributeCode Code, object UntypedValue, NonValidatedPassword Correct, NonValidatedPassword Wrong)> SetupUserWithPasswordAsync(
        IUserAuthenticatorsSelfService selfService,
        IUserProfileSelfService profileSelfService,
        IUserProfileSchemaAdmin schemaAdmin)
    {
        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var (correctPassword, correctSupplied) = await TestData.CreatePasswordPairAsync(selfService, user.SubjectId, _ct);
        var (__, wrongSupplied) = await TestData.CreatePasswordPairAsync(selfService, ct: _ct);

        await TestData.AddAttributeDefinitions(schemaAdmin, _ct);
        var schema = await profileSelfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await profileSelfService.TryRegisterAsync(user.SubjectId, attributes.Validate(), _ct)).ShouldNotBeNull();
        (await selfService.TrySetPasswordAsync(user.SubjectId, correctPassword, _ct)).ShouldBeTrue();

        return (attribute.Code, attribute.UntypedValue, correctSupplied, wrongSupplied);
    }

    [Fact]
    public async Task First_lockout_uses_first_escalating_duration()
    {
        var (sp, passwordAuth, selfService, profileSelfService, schemaAdmin, timeProvider) = await CreateAsync(options =>
        {
            options.Throttling.MaxFailedAttempts = 2;
            options.Throttling.FailureWindow = TimeSpan.FromMinutes(15);
            options.Throttling.ThrottleDuration = TimeSpan.FromMinutes(99); // flat fallback — should not be used
            options.Throttling.EscalatingThrottleDurations =
            [
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(10),
                TimeSpan.FromMinutes(60)
            ];
        });
        await using var dispose1 = sp;

        var (code, value, correct, wrong) = await SetupUserWithPasswordAsync(selfService, profileSelfService, schemaAdmin);

        // Trigger first lockout (2 failures)
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Still blocked immediately after lockout
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Advance past first duration (1 min) but not second (10 min)
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(2));

        // Should be allowed after first duration
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Second_lockout_escalates_to_second_duration()
    {
        var (sp, passwordAuth, selfService, profileSelfService, schemaAdmin, timeProvider) = await CreateAsync(options =>
        {
            options.Throttling.MaxFailedAttempts = 2;
            options.Throttling.FailureWindow = TimeSpan.FromMinutes(15);
            options.Throttling.ThrottleDuration = TimeSpan.FromMinutes(99);
            options.Throttling.EscalatingThrottleDurations =
            [
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(10),
                TimeSpan.FromMinutes(60)
            ];
        });
        await using var dispose2 = sp;

        var (code, value, correct, wrong) = await SetupUserWithPasswordAsync(selfService, profileSelfService, schemaAdmin);

        // First lockout
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Advance past first duration — unlock
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(2));
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Blocked again — second lockout
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Advance past first duration (1 min) — should still be blocked (second lockout uses 10 min)
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(2));
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Advance past second duration (10 min total from second lockout)
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(9));
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Lockout_duration_clamps_to_last_entry_when_lockout_count_exceeds_list()
    {
        var (sp, passwordAuth, selfService, profileSelfService, schemaAdmin, timeProvider) = await CreateAsync(options =>
        {
            options.Throttling.MaxFailedAttempts = 2;
            options.Throttling.FailureWindow = TimeSpan.FromMinutes(15);
            options.Throttling.ThrottleDuration = TimeSpan.FromMinutes(99);
            options.Throttling.EscalatingThrottleDurations =
            [
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5)
            ];
        });
        await using var dispose3 = sp;

        var (code, value, correct, wrong) = await SetupUserWithPasswordAsync(selfService, profileSelfService, schemaAdmin);

        // First lockout — 1 min
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(2));

        // Second lockout — 5 min
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(6));

        // Third lockout — should clamp to last entry (5 min), not exceed list
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Still blocked after 2 min (clamped to 5 min)
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(2));
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Allowed after 5 min
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(4));
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Successful_auth_resets_lockout_count_so_next_lockout_uses_first_duration()
    {
        var (sp, passwordAuth, selfService, profileSelfService, schemaAdmin, timeProvider) = await CreateAsync(options =>
        {
            options.Throttling.MaxFailedAttempts = 2;
            options.Throttling.FailureWindow = TimeSpan.FromMinutes(15);
            options.Throttling.ThrottleDuration = TimeSpan.FromMinutes(99);
            options.Throttling.EscalatingThrottleDurations =
            [
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(10)
            ];
        });
        await using var dispose4 = sp;

        var (code, value, correct, wrong) = await SetupUserWithPasswordAsync(selfService, profileSelfService, schemaAdmin);

        // First lockout
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Unlock after first duration
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(2));

        // Successful auth — resets lockout count
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();

        // New lockout — should use first duration again (1 min), not second (10 min)
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Blocked immediately
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Advance past first duration (1 min) — should be allowed (not 10 min)
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(2));
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Null_escalating_durations_preserves_flat_throttle_behavior()
    {
        var (sp, passwordAuth, selfService, profileSelfService, schemaAdmin, timeProvider) = await CreateAsync(options =>
        {
            options.Throttling.MaxFailedAttempts = 2;
            options.Throttling.FailureWindow = TimeSpan.FromMinutes(15);
            options.Throttling.ThrottleDuration = TimeSpan.FromMinutes(5);
            options.Throttling.EscalatingThrottleDurations = null; // flat behavior
        });
        await using var dispose5 = sp;

        var (code, value, correct, wrong) = await SetupUserWithPasswordAsync(selfService, profileSelfService, schemaAdmin);

        // First lockout
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Blocked immediately
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Advance past flat duration (5 min)
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(6));
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();

        // Second lockout — still uses flat 5 min
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, wrong, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Blocked immediately
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        // Advance past flat duration again
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddMinutes(6));
        _ = (await passwordAuth.TryAuthenticateAsync(code, value, correct, _ct)).ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }
}
