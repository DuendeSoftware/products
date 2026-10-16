// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Private.Licencing.V2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Internal.Licensing;

/// <summary>
/// User Management license validation. Delegates to the shared <see cref="LicenseValidator"/>
/// infrastructure for rate-limited logging and entitlement checks.
/// </summary>
/// <remarks>
/// Disposing the validator cancels a running user count check and waits for it to finish, so no
/// check outlives the service provider that owns the validator.
/// </remarks>
internal sealed class UserManagementLicenseValidator(
    LicenseValidator validator,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<UserManagementLicenseValidator> logger) : IDisposable, IAsyncDisposable
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
    private int _userCountCheckInFlight;
    private Task _userCountCheck = Task.CompletedTask;
    private readonly CancellationTokenSource _stopping = new();
    private int _disposed;
    private static readonly TimeSpan UserCountCheckInterval = TimeSpan.FromHours(1);

    public void Dispose()
    {
        if (BeginDispose())
        {
            // The check catches every exception, so waiting on it cannot throw.
            Volatile.Read(ref _userCountCheck).GetAwaiter().GetResult();
            _stopping.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (BeginDispose())
        {
            await Volatile.Read(ref _userCountCheck);
            _stopping.Dispose();
        }
    }

    private bool BeginDispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return false;
        }

        _stopping.Cancel();
        return true;
    }

    /// <summary>
    /// Starts a background user count check when the throttle window has elapsed and no check is running.
    /// The caller never waits for the check, and the check never fails the caller's operation.
    /// </summary>
    internal void ValidateUserCount() => _ = TryStartUserCountCheck();

    /// <summary>
    /// Starts a background user count check if one is due.
    /// </summary>
    /// <returns>The running check, or <see langword="null"/> when no check was started.</returns>
    internal Task? TryStartUserCountCheck()
    {
        if (_stopping.IsCancellationRequested)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcTicks;
        var last = Interlocked.Read(ref _lastUserCountCheckTicks);

        if (now - last < UserCountCheckInterval.Ticks)
        {
            return null;
        }

        if (Interlocked.CompareExchange(ref _userCountCheckInFlight, 1, 0) != 0)
        {
            return null;
        }

        if (Interlocked.CompareExchange(ref _lastUserCountCheckTicks, now, last) != last)
        {
            _ = Interlocked.Exchange(ref _userCountCheckInFlight, 0);
            return null;
        }

        var check = Task.Run(CheckUserCountAsync);
        _ = Interlocked.Exchange(ref _userCountCheck, check);
        return check;
    }

    private async Task CheckUserCountAsync()
    {
        long count = 0;
        Exception? failure = null;

        try
        {
            var ct = _stopping.Token;
            await using var scope = scopeFactory.CreateAsyncScope();
            count = await scope.ServiceProvider.GetRequiredService<IUserCountProvider>()
                .GetUserCountAsync(ct);

            validator.ValidateQuantized(SkuIds.UM_001, (int)Math.Min(count, int.MaxValue));
        }
#pragma warning disable CA1031 // Licence enforcement is soft; a failed count must never fail user operations.
        catch (Exception) when (_stopping.IsCancellationRequested)
        {
            // The validator is being disposed, so the check is abandoned without reporting a failure.
            return;
        }
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // No partial total is validated. The next throttle window retries.
            failure = ex;
        }
        finally
        {
            _ = Interlocked.Exchange(ref _userCountCheckInFlight, 0);
        }

        if (failure is { } exception)
        {
            logger.UserCountCheckFailed(exception);
        }
        else
        {
            logger.UserCountCheckCompleted(count);
        }
    }
}
