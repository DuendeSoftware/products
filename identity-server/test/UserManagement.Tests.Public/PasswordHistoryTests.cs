// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Passwords;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class PasswordHistoryTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static async Task<ServiceProvider> CreateServiceProviderAsync(int historyCount) =>
        await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passwords.HistoryCount = historyCount);

    private static async Task<UserSubjectId> RegisterUserAsync(IExternalAuthenticator externalAuthenticator, Ct ct)
    {
        var subjectId = await externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), ct);
        return subjectId;
    }

    [Fact]
    public async Task Cannot_reuse_current_password()
    {
        await using var sp = await CreateServiceProviderAsync(historyCount: 3);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var userId = await RegisterUserAsync(sp.GetRequiredService<IExternalAuthenticator>(), _ct);
        var raw1 = $"ABcd12!@{Guid.NewGuid()}";
        var password1 = await selfService.ValidatePasswordAsync(userId, raw1, _ct);

        // Set initial password
        (await selfService.TrySetPasswordAsync(userId, password1, _ct)).ShouldBeTrue();

        // Attempting to "change" to the same current password should fail
        var reusedResult = await selfService.TryValidatePasswordAsync(userId, raw1, _ct);
        var failed = reusedResult.ShouldBeOfType<PasswordCreationResult.Failed>();
        failed.Errors.ShouldContain("Password has been used recently and cannot be reused.");
    }

    [Fact]
    public async Task Cannot_reuse_password_in_history()
    {
        await using var sp = await CreateServiceProviderAsync(historyCount: 3);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var userId = await RegisterUserAsync(sp.GetRequiredService<IExternalAuthenticator>(), _ct);
        var raw1 = $"ABcd12!@{Guid.NewGuid()}";
        var password1 = await selfService.ValidatePasswordAsync(userId, raw1, _ct);

        // Set initial password
        (await selfService.TrySetPasswordAsync(userId, password1, _ct)).ShouldBeTrue();

        // Change to a second password — this pushes password1 into history
        var raw2 = $"ABcd12!@{Guid.NewGuid()}";
        var password2 = await selfService.ValidatePasswordAsync(userId, raw2, _ct);
        (await selfService.TryResetPasswordAsync(userId, password2, _ct)).ShouldBeTrue();

        // Attempting to reuse password1 (now in history) should fail
        var reusedResult = await selfService.TryValidatePasswordAsync(userId, raw1, _ct);
        var failed = reusedResult.ShouldBeOfType<PasswordCreationResult.Failed>();
        failed.Errors.ShouldContain("Password has been used recently and cannot be reused.");
    }

    [Fact]
    public async Task Password_pushed_out_of_history_window_can_be_reused()
    {
        await using var sp = await CreateServiceProviderAsync(historyCount: 3);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var userId = await RegisterUserAsync(sp.GetRequiredService<IExternalAuthenticator>(), _ct);

        // Set initial password (password1)
        var raw1 = $"ABcd12!@{Guid.NewGuid()}";
        var password1 = await selfService.ValidatePasswordAsync(userId, raw1, _ct);
        (await selfService.TrySetPasswordAsync(userId, password1, _ct)).ShouldBeTrue();

        // Change through 4 more passwords to push password1 out of the history window (historyCount=3)
        for (var i = 0; i < 4; i++)
        {
            var rawN = $"ABcd12!@{Guid.NewGuid()}";
            var passwordN = await selfService.ValidatePasswordAsync(userId, rawN, _ct);
            (await selfService.TryResetPasswordAsync(userId, passwordN, _ct)).ShouldBeTrue();
        }

        // password1 should now be outside the 3-entry window and allowed again
        var reusedResult = await selfService.TryValidatePasswordAsync(userId, raw1, _ct);
        _ = reusedResult.ShouldBeOfType<PasswordCreationResult.Success>();
    }

    [Fact]
    public async Task History_count_zero_disables_history_check()
    {
        await using var sp = await CreateServiceProviderAsync(historyCount: 0);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var userId = await RegisterUserAsync(sp.GetRequiredService<IExternalAuthenticator>(), _ct);
        var raw1 = $"ABcd12!@{Guid.NewGuid()}";
        var password1 = await selfService.ValidatePasswordAsync(userId, raw1, _ct);

        (await selfService.TrySetPasswordAsync(userId, password1, _ct)).ShouldBeTrue();

        // Reset to a different password
        var raw2 = $"ABcd12!@{Guid.NewGuid()}";
        var password2 = await selfService.ValidatePasswordAsync(userId, raw2, _ct);
        (await selfService.TryResetPasswordAsync(userId, password2, _ct)).ShouldBeTrue();

        // With HistoryCount=0, reusing raw1 should be allowed
        var reusedResult = await selfService.TryValidatePasswordAsync(userId, raw1, _ct);
        _ = reusedResult.ShouldBeOfType<PasswordCreationResult.Success>();
    }

    [Fact]
    public async Task Fresh_password_not_in_history_is_allowed()
    {
        await using var sp = await CreateServiceProviderAsync(historyCount: 3);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var userId = await RegisterUserAsync(sp.GetRequiredService<IExternalAuthenticator>(), _ct);
        var raw1 = $"ABcd12!@{Guid.NewGuid()}";
        var password1 = await selfService.ValidatePasswordAsync(userId, raw1, _ct);
        (await selfService.TrySetPasswordAsync(userId, password1, _ct)).ShouldBeTrue();

        // A brand-new password string not previously used should be accepted
        var rawNew = $"ABcd12!@{Guid.NewGuid()}";
        var newResult = await selfService.TryValidatePasswordAsync(userId, rawNew, _ct);
        _ = newResult.ShouldBeOfType<PasswordCreationResult.Success>();
    }
}
