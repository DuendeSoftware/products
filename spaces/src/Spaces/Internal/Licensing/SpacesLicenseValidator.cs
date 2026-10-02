// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Private.Licencing.V2;

namespace Duende.Spaces.Internal.Licensing;

/// <summary>
/// Spaces license validation. Delegates to the shared <see cref="LicenseValidator"/>
/// infrastructure for rate-limited logging and entitlement checks.
/// </summary>
internal sealed class SpacesLicenseValidator(LicenseValidator validator)
{
    internal bool ValidateSpaces() => validator.ValidateFeature(SkuIds.PLT_024);

    /// <summary>
    /// Soft license check. Called on the success path of admin operations that can raise
    /// the active space count. Neither the count delegate nor the quantized validator may
    /// fail the admin operation; both are exception-isolated.
    /// </summary>
    internal async Task ValidateSpaceCountAsync(Func<Ct, Task<long>> getActiveCount, Ct ct)
    {
        try
        {
            var raw = await getActiveCount(ct);
            var count = (int)Math.Min(raw, int.MaxValue);
            validator.ValidateQuantized(SkuIds.PLT_023, count);
        }
#pragma warning disable CA1031
        catch
#pragma warning restore CA1031
        {
            // License validation is soft, swallow failures silently.
            // The next admin operation will retry.
        }
    }

#pragma warning disable CA2201
    internal static void ThrowInvalidLicenseException(string message) => throw new Exception(message);
#pragma warning restore CA2201
}
