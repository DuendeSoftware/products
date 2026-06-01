// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class PasswordExpirationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static async Task<ServiceProvider> CreateServiceProviderAsync(int? maxAgeDays) =>
        await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
            options.Passwords.MaxAgeDays = maxAgeDays);

    private static async Task<(UserSubjectId SubjectId, AttributeCode Code, object Value)> RegisterUserWithPasswordAsync(
        IUserAuthenticatorsSelfService selfService,
        IUserProfileSelfService profileSelfService,
        IUserProfileSchemaAdmin schemaAdmin,
        IExternalAuthenticator externalAuthenticator,
        Ct ct)
    {
        var subjectId = await externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), ct);

        await TestData.AddAttributeDefinitions(schemaAdmin, ct);
        var schema = await profileSelfService.GetSchemaAsync(ct);
        var attributes = TestData.CreateAttributes(schema);
        var attribute = attributes.ElementAt(0);
        _ = (await profileSelfService.TryCreateAsync(subjectId, attributes.Validate(), ct)).ShouldNotBeNull();

        return (subjectId, attribute.Code, attribute.UntypedValue);
    }

    [Fact]
    public async Task Non_expired_password_returns_Success()
    {
        await using var sp = await CreateServiceProviderAsync(maxAgeDays: 30);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();
        var profileSelfService = sp.GetRequiredService<IUserProfileSelfService>();
        var schemaAdmin = sp.GetRequiredService<IUserProfileSchemaAdmin>();
        var passwordAuthenticator = sp.GetRequiredService<IPasswordAuthenticator>();
        var timeProvider = sp.GetRequiredService<FakeTimeProvider>();

        var (subjectId, code, value) = await RegisterUserWithPasswordAsync(selfService, profileSelfService, schemaAdmin, sp.GetRequiredService<IExternalAuthenticator>(), _ct);
        var (password, supplied) = await TestData.CreatePasswordPairAsync(selfService, subjectId, _ct);
        (await selfService.TrySetPasswordAsync(subjectId, password, _ct)).ShouldBeTrue();

        // Advance time by 10 days — still within the 30-day window
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddDays(10));

        var result = await passwordAuthenticator.TryAuthenticateAsync(code, value, supplied, _ct);
        var success = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
        success.UserSubjectId.ShouldBe(subjectId);
    }

    [Fact]
    public async Task Expired_password_returns_Expired_with_correct_SubjectId()
    {
        await using var sp = await CreateServiceProviderAsync(maxAgeDays: 30);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();
        var profileSelfService = sp.GetRequiredService<IUserProfileSelfService>();
        var schemaAdmin = sp.GetRequiredService<IUserProfileSchemaAdmin>();
        var passwordAuthenticator = sp.GetRequiredService<IPasswordAuthenticator>();
        var timeProvider = sp.GetRequiredService<FakeTimeProvider>();

        var (subjectId, code, value) = await RegisterUserWithPasswordAsync(selfService, profileSelfService, schemaAdmin, sp.GetRequiredService<IExternalAuthenticator>(), _ct);
        var (password, supplied) = await TestData.CreatePasswordPairAsync(selfService, subjectId, _ct);
        (await selfService.TrySetPasswordAsync(subjectId, password, _ct)).ShouldBeTrue();

        // Advance time past the 30-day expiry
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddDays(31));

        var result = await passwordAuthenticator.TryAuthenticateAsync(code, value, supplied, _ct);
        var expired = result.ShouldBeOfType<PasswordAuthenticationResult.Expired>();
        expired.UserSubjectId.ShouldBe(subjectId);
    }

    [Fact]
    public async Task Null_MaxAgeDays_never_expires()
    {
        await using var sp = await CreateServiceProviderAsync(maxAgeDays: null);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();
        var profileSelfService = sp.GetRequiredService<IUserProfileSelfService>();
        var schemaAdmin = sp.GetRequiredService<IUserProfileSchemaAdmin>();
        var passwordAuthenticator = sp.GetRequiredService<IPasswordAuthenticator>();
        var timeProvider = sp.GetRequiredService<FakeTimeProvider>();

        var (subjectId, code, value) = await RegisterUserWithPasswordAsync(selfService, profileSelfService, schemaAdmin, sp.GetRequiredService<IExternalAuthenticator>(), _ct);
        var (password, supplied) = await TestData.CreatePasswordPairAsync(selfService, subjectId, _ct);
        (await selfService.TrySetPasswordAsync(subjectId, password, _ct)).ShouldBeTrue();

        // Advance time by many years
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddDays(3650));

        var result = await passwordAuthenticator.TryAuthenticateAsync(code, value, supplied, _ct);
        _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Changing_password_resets_expiry_clock()
    {
        await using var sp = await CreateServiceProviderAsync(maxAgeDays: 30);
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();
        var profileSelfService = sp.GetRequiredService<IUserProfileSelfService>();
        var schemaAdmin = sp.GetRequiredService<IUserProfileSchemaAdmin>();
        var passwordAuthenticator = sp.GetRequiredService<IPasswordAuthenticator>();
        var timeProvider = sp.GetRequiredService<FakeTimeProvider>();

        var (subjectId, code, value) = await RegisterUserWithPasswordAsync(selfService, profileSelfService, schemaAdmin, sp.GetRequiredService<IExternalAuthenticator>(), _ct);
        var (password, supplied) = await TestData.CreatePasswordPairAsync(selfService, subjectId, _ct);
        (await selfService.TrySetPasswordAsync(subjectId, password, _ct)).ShouldBeTrue();

        // Advance time past the 30-day expiry
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddDays(31));

        // Confirm it's expired
        var expiredResult = await passwordAuthenticator.TryAuthenticateAsync(code, value, supplied, _ct);
        _ = expiredResult.ShouldBeOfType<PasswordAuthenticationResult.Expired>();

        // Reset the password (admin reset, not change — since change requires old password auth which would also be expired)
        var (newPassword, newSupplied) = await TestData.CreatePasswordPairAsync(selfService, subjectId, _ct);
        (await selfService.TryResetPasswordAsync(subjectId, newPassword, _ct)).ShouldBeTrue();

        // Advance time by 10 more days — still within the new 30-day window
        timeProvider.SetUtcNow(timeProvider.GetUtcNow().AddDays(10));

        var result = await passwordAuthenticator.TryAuthenticateAsync(code, value, newSupplied, _ct);
        _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }
}
