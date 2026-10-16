// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Membership;
using Microsoft.Extensions.DependencyInjection;
using Skus = Duende.UserManagement.TestUserManagementLicense.Skus;

namespace Duende.Platform.UserManagement.Licensing;

public sealed class LicenseEntitlementTests
{
    private static Ct Ct => TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);

    private static readonly Dictionary<string, Func<UserManagementLicenseValidator, bool>> FeatureChecks = new()
    {
        { Skus.Profiles, v => v.ValidateProfiles() },
        { Skus.RolesAndGroups, v => v.ValidateRolesAndGroups() },
        { Skus.Otp, v => v.ValidateOtp() },
        { Skus.Totp, v => v.ValidateTotp() },
        { Skus.Passkey, v => v.ValidatePasskey() },
        { Skus.RecoveryCode, v => v.ValidateRecoveryCode() },
        { Skus.Password, v => v.ValidatePassword() },
        { Skus.AccountLockout, v => v.ValidateAccountLockout() },
        { Skus.ExternalIdpLinking, v => v.ValidateExternalIdpLinking() },
        { Skus.SelfService, v => v.ValidateSelfService() },
        { Skus.Administration, v => v.ValidateAdministration() },
        { Skus.Registration, v => v.ValidateRegistration() },
        { Skus.UserEvents, v => v.ValidateUserEvents() },
        { Skus.AdvancedPasswordPolicies, v => v.ValidateAdvancedPasswordPolicies() },
        { Skus.PerSpacePolicies, v => v.ValidatePerSpacePolicies() },
        { Skus.InboundScim, v => v.ValidateInboundScim() },
    };

    public static TheoryData<string> FeatureSkus => [.. FeatureChecks.Keys];

    [Theory]
    [MemberData(nameof(FeatureSkus))]
    public void Without_sku_denies_only_that_feature(string sku)
    {
        using var services = BuildValidatorServices(TestUserManagementLicense.Create().Without(sku));
        var validator = services.GetRequiredService<UserManagementLicenseValidator>();

        foreach (var (featureSku, check) in FeatureChecks)
        {
            check(validator).ShouldBe(featureSku != sku, $"Feature check for {featureSku}");
        }
    }

    [Fact]
    public async Task RoleAdmin_CreateAsync_without_RolesAndGroups_sku_throws()
    {
        await using var services = await UsersServiceProviderFactory.CreateAsync(
            s => TestUserManagementLicense.Create().Without(Skus.RolesAndGroups).Register(s, _time),
            _ => { });
        var admin = services.GetRequiredService<IRoleAdmin>();

        var ex = await Should.ThrowAsync<Exception>(() => admin.CreateAsync(NewRole(), Ct));

        ex.Message.ShouldContain("Roles and Groups");
    }

    [Fact]
    public void Without_unknown_sku_throws() =>
        _ = Should.Throw<ArgumentException>(() => TestUserManagementLicense.Create().Without("UM_999"));

    private ServiceProvider BuildValidatorServices(TestUserManagementLicense license)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<TimeProvider>(_time);
        LicensingModule.Register(services);
        _ = license.Register(services, _time);
        return services.BuildServiceProvider();
    }

    private static Role NewRole() => new() { Name = RoleName.Create($"role-{Guid.NewGuid():N}") };
}
