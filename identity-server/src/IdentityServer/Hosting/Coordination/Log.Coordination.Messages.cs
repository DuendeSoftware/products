// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.Coordination;

internal static partial class Log
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        EventName = nameof(JobLockUnavailable),
        Message = "Could not reach the distributed lock for job {LockName}; skipping this run")]
    internal static partial void JobLockUnavailable(this ILogger logger, Exception exception, string lockName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        EventName = nameof(JobLeaseLost),
        Message = "Job {LockName} lost its lease while running; another node may run it next")]
    internal static partial void JobLeaseLost(this ILogger logger, string lockName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        EventName = nameof(JobLeaseTimingInvalid),
        Message = "Lease timing for job {LockName} was invalid ({RenewalInterval} renewal, {CancelBeforeExpiry} cancel-before-expiry, {Duration} duration); using clamped values instead")]
    internal static partial void JobLeaseTimingInvalid(this ILogger logger, string lockName, TimeSpan renewalInterval, TimeSpan cancelBeforeExpiry, TimeSpan duration);
}
