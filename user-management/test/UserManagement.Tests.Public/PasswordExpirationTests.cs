// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passwords;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class PasswordExpirationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static async Task<ServiceProvider> CreateServiceProviderAsync(int? maxAgeDays) =>
        await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passwords.MaxAgeDays = maxAgeDays);

    private static async Task<(UserSubjectId SubjectId, UserName UserName)> RegisterUserWithPasswordAsync(
        IUserAuthenticatorsSelfService selfService,
        IUserSelfService userSelfService,
        Ct ct)
    {
        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), ct)).ShouldNotBeNull();
        var userName = TestData.CreateUserName();
        (await userSelfService.TrySetUserNameAsync(user.SubjectId, userName, ct)).ShouldBeTrue();
        return (user.SubjectId, userName);
    }

    [Fact]
    public async Task Non_expired_password_returns_Success()
    {
        await using var sp = await CreateServiceProviderAsync(maxAgeDays: 30);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();
        var userSelfService = sp.GetRequiredService<IUserSelfService>();
        var passwordAuth = sp.GetRequiredService<IPasswordAuth>();
        var timeProvider = sp.GetRequiredService<FakeTimeProvider>();

        var (subjectId, userName) = await RegisterUserWithPasswordAsync(selfService, userSelfService, _ct);
        var (password, supplied) = await TestData.CreatePasswordPairAsync(selfService, subjectId, _ct);
        (await selfService.TrySetPasswordAsync(subjectId, password, _ct)).ShouldBeTrue();

        // Advance time by 10 days — still within the 30-day window
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddDays(10));

        var result = await passwordAuth.TryAuthenticateAsync(userName, supplied, _ct);
        var success = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
        success.UserSubjectId.ShouldBe(subjectId);
    }

    [Fact]
    public async Task Expired_password_returns_Expired_with_correct_SubjectId()
    {
        await using var sp = await CreateServiceProviderAsync(maxAgeDays: 30);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();
        var userSelfService = sp.GetRequiredService<IUserSelfService>();
        var passwordAuth = sp.GetRequiredService<IPasswordAuth>();
        var timeProvider = sp.GetRequiredService<FakeTimeProvider>();

        var (subjectId, userName) = await RegisterUserWithPasswordAsync(selfService, userSelfService, _ct);
        var (password, supplied) = await TestData.CreatePasswordPairAsync(selfService, subjectId, _ct);
        (await selfService.TrySetPasswordAsync(subjectId, password, _ct)).ShouldBeTrue();

        // Advance time past the 30-day expiry
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddDays(31));

        var result = await passwordAuth.TryAuthenticateAsync(userName, supplied, _ct);
        var expired = result.ShouldBeOfType<PasswordAuthenticationResult.Expired>();
        expired.UserSubjectId.ShouldBe(subjectId);
    }

    [Fact]
    public async Task Null_MaxAgeDays_never_expires()
    {
        await using var sp = await CreateServiceProviderAsync(maxAgeDays: null);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();
        var userSelfService = sp.GetRequiredService<IUserSelfService>();
        var passwordAuth = sp.GetRequiredService<IPasswordAuth>();
        var timeProvider = sp.GetRequiredService<FakeTimeProvider>();

        var (subjectId, userName) = await RegisterUserWithPasswordAsync(selfService, userSelfService, _ct);
        var (password, supplied) = await TestData.CreatePasswordPairAsync(selfService, subjectId, _ct);
        (await selfService.TrySetPasswordAsync(subjectId, password, _ct)).ShouldBeTrue();

        // Advance time by many years
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddDays(3650));

        var result = await passwordAuth.TryAuthenticateAsync(userName, supplied, _ct);
        _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Changing_password_resets_expiry_clock()
    {
        await using var sp = await CreateServiceProviderAsync(maxAgeDays: 30);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();
        var userSelfService = sp.GetRequiredService<IUserSelfService>();
        var passwordAuth = sp.GetRequiredService<IPasswordAuth>();
        var timeProvider = sp.GetRequiredService<FakeTimeProvider>();

        var (subjectId, userName) = await RegisterUserWithPasswordAsync(selfService, userSelfService, _ct);
        var (password, supplied) = await TestData.CreatePasswordPairAsync(selfService, subjectId, _ct);
        (await selfService.TrySetPasswordAsync(subjectId, password, _ct)).ShouldBeTrue();

        // Advance time past the 30-day expiry
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddDays(31));

        // Confirm it's expired
        var expiredResult = await passwordAuth.TryAuthenticateAsync(userName, supplied, _ct);
        _ = expiredResult.ShouldBeOfType<PasswordAuthenticationResult.Expired>();

        // Reset the password (admin reset, not change — since change requires old password auth which would also be expired)
        var (newPassword, newSupplied) = await TestData.CreatePasswordPairAsync(selfService, subjectId, _ct);
        (await selfService.TryResetPasswordAsync(subjectId, newPassword, _ct)).ShouldBeTrue();

        // Advance time by 10 more days — still within the new 30-day window
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddDays(10));

        var result = await passwordAuth.TryAuthenticateAsync(userName, newSupplied, _ct);
        _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }
}
