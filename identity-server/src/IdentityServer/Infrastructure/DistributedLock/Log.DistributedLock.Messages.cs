// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal.Operations;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

internal static partial class Log
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        EventName = nameof(LockAcquired),
        Message = "Acquired lock {LockName} (version {Version})")]
    internal static partial void LockAcquired(this ILogger logger, string lockName, int version);

    [LoggerMessage(
        Level = LogLevel.Trace,
        EventName = nameof(LockBusy),
        Message = "Lock {LockName} is busy; retrying")]
    internal static partial void LockBusy(this ILogger logger, string lockName);

    [LoggerMessage(
        Level = LogLevel.Debug,
        EventName = nameof(LockNotAcquiredApplicationStopping),
        Message = "Lock {LockName} not acquired: application stopping")]
    internal static partial void LockNotAcquiredApplicationStopping(this ILogger logger, string lockName);

    [LoggerMessage(
        Level = LogLevel.Trace,
        EventName = nameof(LeaseRenewed),
        Message = "Renewed lease for lock {LockName} (version {Version})")]
    internal static partial void LeaseRenewed(this ILogger logger, string lockName, int version);

    [LoggerMessage(
        Level = LogLevel.Warning,
        EventName = nameof(LeaseRenewalFailed),
        Message = "Failed to renew lease for lock {LockName}; will retry until it would expire")]
    internal static partial void LeaseRenewalFailed(this ILogger logger, Exception exception, string lockName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        EventName = nameof(LeaseLost),
        Message = "Lost lease for lock {LockName}")]
    internal static partial void LeaseLost(this ILogger logger, string lockName);

    [LoggerMessage(
        Level = LogLevel.Debug,
        EventName = nameof(LeaseReleased),
        Message = "Released lease for lock {LockName}")]
    internal static partial void LeaseReleased(this ILogger logger, string lockName);

    [LoggerMessage(
        Level = LogLevel.Debug,
        EventName = nameof(LeaseReleaseNotSuccessful),
        Message = "Releasing lease for lock {LockName} returned {UpdateResult}")]
    internal static partial void LeaseReleaseNotSuccessful(this ILogger logger, string lockName, UpdateResult updateResult);

    [LoggerMessage(
        Level = LogLevel.Warning,
        EventName = nameof(LeaseReleaseFailed),
        Message = "Failed to release lease for lock {LockName}")]
    internal static partial void LeaseReleaseFailed(this ILogger logger, Exception exception, string lockName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        EventName = nameof(ReleasingLeakedLeaseAtShutdown),
        Message = "Lease for lock {LockName} was not disposed by its owner; releasing it at shutdown")]
    internal static partial void ReleasingLeakedLeaseAtShutdown(this ILogger logger, string lockName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        EventName = nameof(LeasesNotReleasedOnSynchronousDispose),
        Message = "{LeaseCount} lease(s) were still held when the distributed lock was disposed synchronously (IDisposable.Dispose); they were not released and will expire on their own. Dispose the lock asynchronously (IAsyncDisposable.DisposeAsync) to release them immediately")]
    internal static partial void LeasesNotReleasedOnSynchronousDispose(this ILogger logger, int leaseCount);
}
