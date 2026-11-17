// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Private.Licencing.V2;

namespace Duende.Spaces.Internal.Licensing;

/// <summary>
/// Spaces license validation. Delegates to the shared <see cref="LicenseValidator"/>
/// infrastructure for rate-limited logging and entitlement checks.
/// </summary>
internal sealed class SpacesLicenseValidator(LicenseValidator validator, TimeProvider timeProvider)
{
    private static readonly TimeSpan SpaceCountWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SpaceCountTimeout = TimeSpan.FromSeconds(5);
    private long _spaceCountWindowStartTicks;

    internal bool ValidateSpaces() => validator.ValidateFeature(SkuIds.PLT_024);

    /// <summary>
    /// Soft license check. Called on the success path of admin operations that can raise
    /// the active space count, and when a request resolves a space. Counts at most once per
    /// 30 minutes, the licensing package's default log window. Neither the count delegate nor
    /// the quantized validator may fail the caller; both are exception-isolated.
    /// </summary>
    internal async Task ValidateSpaceCountAsync(Func<Ct, Task<long>> getActiveCount, Ct ct)
    {
        var now = timeProvider.GetUtcNow().UtcTicks;
        var windowStart = _spaceCountWindowStartTicks;
        if (now - windowStart < SpaceCountWindow.Ticks
            || Interlocked.CompareExchange(ref _spaceCountWindowStartTicks, now, windowStart) != windowStart)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(SpaceCountTimeout, timeProvider);
            using var countCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            var raw = await getActiveCount(countCancellation.Token);
            var count = (int)Math.Min(raw, int.MaxValue);
            validator.ValidateQuantized(SkuIds.PLT_023, count);
        }
#pragma warning disable CA1031
        catch
#pragma warning restore CA1031
        {
            // License validation is soft, swallow failures silently.
            // The first call after the window retries.
        }
        finally
        {
            _spaceCountWindowStartTicks = timeProvider.GetUtcNow().UtcTicks;
        }
    }

#pragma warning disable CA2201
    internal static void ThrowInvalidLicenseException(string message) => throw new Exception(message);
#pragma warning restore CA2201
}
