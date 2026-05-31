// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Private.Licencing.V2;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// License validation for IdentityServer.Configuration. Delegates to the shared
/// <see cref="LicenseValidator"/> infrastructure for rate-limited logging and entitlement checks.
/// </summary>
internal sealed class IdentityServerConfigurationLicenseValidator(LicenseValidator validator)
{
    internal void ValidateDynamicClientRegistration() => validator.ValidateFeature(SkuIds.PTC_021);
}
