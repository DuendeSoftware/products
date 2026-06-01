// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Private.Licencing.V2;
using Duende.UserManagement.Internal.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.Internal.Licensing;

/// <summary>
/// User Management license validation. Delegates to the shared <see cref="LicenseValidator"/>
/// infrastructure for rate-limited logging and entitlement checks.
/// </summary>
internal sealed class UserManagementLicenseValidator(
    LicenseValidator validator,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider)
{
    internal bool ValidateProfiles() => validator.ValidateFeature(SkuIds.UM_002);

    internal bool ValidateRolesAndGroups() => validator.ValidateFeature(SkuIds.UM_003);

    internal bool ValidateOtp() => validator.ValidateFeature(SkuIds.UM_004);

    internal bool ValidateTotp() => validator.ValidateFeature(SkuIds.UM_005);

    internal bool ValidatePasskey() => validator.ValidateFeature(SkuIds.UM_006);

    internal bool ValidateRecoveryCode() => validator.ValidateFeature(SkuIds.UM_007);

    internal bool ValidatePassword() => validator.ValidateFeature(SkuIds.UM_008);

    // TODO: UM-009 (Account Lockout) - deferred. Injection point is DefaultAuthenticationAttemptPolicy.EvaluateAsync
    //       but the relationship with UM-016 (Per-space Policies) needs clarification.
    internal bool ValidateAccountLockout() => validator.ValidateFeature(SkuIds.UM_009);

    internal bool ValidateExternalIdpLinking() => validator.ValidateFeature(SkuIds.UM_010);

    internal bool ValidateSelfService() => validator.ValidateFeature(SkuIds.UM_011);

    internal bool ValidateAdministration() => validator.ValidateFeature(SkuIds.UM_012);

    internal bool ValidateRegistration() => validator.ValidateFeature(SkuIds.UM_013);

    internal bool ValidateInboundScim() => validator.ValidateFeature(SkuIds.PLT_013);

    // TODO: UM-014 (User Events) - deferred. We need to establish when this needs to be included.
    internal bool ValidateUserEvents() => validator.ValidateFeature(SkuIds.UM_014);

    // TODO: UM-015 (Advanced Password Policies) - deferred. Find the appropriate place to call this.
    internal bool ValidateAdvancedPasswordPolicies() => validator.ValidateFeature(SkuIds.UM_015);

    // TODO: UM-016 (Per-space Policies) - deferred. Same injection point as UM-009
    //       (DefaultAuthenticationAttemptPolicy.EvaluateAsync). Needs design clarification.
    internal bool ValidatePerSpacePolicies() => validator.ValidateFeature(SkuIds.UM_016);

#pragma warning disable CA2201
    internal static void ThrowInvalidLicenseException(string message) => throw new Exception(message);
#pragma warning restore CA2201

    private long _lastUserCountCheckTicks;
    private static readonly TimeSpan UserCountCheckInterval = TimeSpan.FromHours(1);

    internal void ValidateUserCount()
    {
        var now = timeProvider.GetUtcNow().UtcTicks;
        var last = Interlocked.Read(ref _lastUserCountCheckTicks);

        if (now - last < UserCountCheckInterval.Ticks)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastUserCountCheckTicks, now, last) != last)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();
                var count = (int)Math.Min(await userRepository.GetCountAsync(CancellationToken.None), int.MaxValue);
                validator.ValidateQuantized(SkuIds.UM_001, count);
            }
#pragma warning disable CA1031
            catch
#pragma warning restore CA1031
            {
                // License validation is soft, swallow failures silently.
                // The next throttle window will retry.
            }
        });
    }
}
