// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Licensing.Enforcement;
using Duende.Private.Licencing.V2;

namespace Duende.UserManagement.Internal.Licensing;

/// <summary>
/// User Management license validation. Delegates to the shared <see cref="LicenseValidator"/>
/// infrastructure for rate-limited logging and entitlement checks.
/// </summary>
internal sealed class UserManagementLicenseValidator(LicenseValidator validator)
{
    internal void ValidateProfiles() => validator.ValidateFeature(SkuIds.UM_002, "User Profiles");

    internal void ValidateRolesAndGroups() => validator.ValidateFeature(SkuIds.UM_003, "Roles & Groups");

    internal void ValidateOtp() => validator.ValidateFeature(SkuIds.UM_004, "OTP Authentication");

    internal void ValidateTotp() => validator.ValidateFeature(SkuIds.UM_005, "TOTP Authentication");

    internal void ValidatePasskey() => validator.ValidateFeature(SkuIds.UM_006, "Passkey Authentication");

    internal void ValidateRecoveryCode() => validator.ValidateFeature(SkuIds.UM_007, "Recovery Code Authentication");

    internal void ValidatePassword() => validator.ValidateFeature(SkuIds.UM_008, "Password Authentication");

    // TODO: UM-009 (Account Lockout) - deferred. Injection point is DefaultAuthenticationAttemptPolicy.EvaluateAsync
    //       but the relationship with UM-016 (Per-space Policies) needs clarification.
    internal void ValidateAccountLockout() => validator.ValidateFeature(SkuIds.UM_009, "Account Lockout");

    internal void ValidateExternalIdpLinking() => validator.ValidateFeature(SkuIds.UM_010, "External IdP Account Linking");

    internal void ValidateSelfService() => validator.ValidateFeature(SkuIds.UM_011, "Self-Service");

    internal void ValidateAdministration() => validator.ValidateFeature(SkuIds.UM_012, "Account Administration");

    internal void ValidateRegistration() => validator.ValidateFeature(SkuIds.UM_013, "Registration Modes");

    // TODO: UM-014 (User Events) - deferred. We need to establish when this needs to be included.
    internal void ValidateUserEvents() => validator.ValidateFeature(SkuIds.UM_014, "User Events");

    // TODO: UM-015 (Advanced Password Policies) - deferred. Find the appropriate place to call this.
    internal void ValidateAdvancedPasswordPolicies() => validator.ValidateFeature(SkuIds.UM_015, "Advanced Password Policies");

    // TODO: UM-016 (Per-space Policies) - deferred. Same injection point as UM-009
    //       (DefaultAuthenticationAttemptPolicy.EvaluateAsync). Needs design clarification.
    internal void ValidatePerSpacePolicies() => validator.ValidateFeature(SkuIds.UM_016, "Per-space Authentication Policies");

    // TODO: We need to design and discuss a mechanism for how we want to retrieve the actual user count.
    internal void ValidateUserCount(int actual) => validator.ValidateQuantized(SkuIds.UM_001, "User count", actual);
}
