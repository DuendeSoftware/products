// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class DefaultAuthenticationAttemptPolicy(
    IOptions<UserAuthenticationOptions> options,
    TimeProvider timeProvider) : IAuthenticationAttemptPolicy
{
    public Task<AuthenticationAttemptDecision> EvaluateAsync(AuthenticationAttemptContext context, Ct ct)
    {
        var throttling = options.Value.Throttling;
        var attemptInfo = context.AttemptInfo;
        var now = timeProvider.GetUtcNow();

        // --- Failure-based throttling ---
        if (attemptInfo.LastFailedAtUtc is not null
            && now >= attemptInfo.LastFailedAtUtc.Value + throttling.FailureWindow)
        {
            // Failure window has elapsed — failure count is stale, allow (velocity check still applies below)
            return Task.FromResult(EvaluateVelocityThrottling(now, throttling, attemptInfo));
        }

        if (attemptInfo.FailedAttemptCount < throttling.MaxFailedAttempts)
        {
            // Under failure threshold — allow (velocity check still applies below)
            return Task.FromResult(EvaluateVelocityThrottling(now, throttling, attemptInfo));
        }

        if (attemptInfo.LastFailedAtUtc is null)
        {
            // Last failed timestamp is missing, but failure count exceeds threshold — reject to be safe
            return Task.FromResult<AuthenticationAttemptDecision>(new AuthenticationAttemptDecision.Reject());
        }

        var blockedUntil = attemptInfo.LastFailedAtUtc.Value + GetThrottleDuration(throttling, attemptInfo.LockoutCount);
        if (now < blockedUntil)
        {
            return Task.FromResult<AuthenticationAttemptDecision>(new AuthenticationAttemptDecision.Reject());
        }

        if (!attemptInfo.RecentAttemptTimestamps.Any())
        {
            // No recent attempts. That means we don't need to throttle based on velocity, and the failure window has elapsed, so we can allow.
            return Task.FromResult<AuthenticationAttemptDecision>(new AuthenticationAttemptDecision.Allow());
        }

        return Task.FromResult(EvaluateVelocityThrottling(now, throttling, attemptInfo));
    }

    private static TimeSpan GetThrottleDuration(AuthenticationThrottlingOptions throttling, int lockoutCount)
    {
        var escalating = throttling.EscalatingThrottleDurations;
        if (escalating is null || escalating.Count == 0)
        {
            return throttling.ThrottleDuration;
        }

        var index = lockoutCount > 0 ? lockoutCount - 1 : 0;
        index = index < escalating.Count ? index : escalating.Count - 1;
        return escalating[index];
    }

    private static AuthenticationAttemptDecision EvaluateVelocityThrottling(DateTimeOffset now, AuthenticationThrottlingOptions throttling,
        AuthenticatorAttemptInfo attemptInfo)
    {
        if (attemptInfo.RecentAttemptTimestamps.Count == 0)
        {
            return new AuthenticationAttemptDecision.Allow();
        }

        var effectiveWindow = throttling.VelocityWindow > throttling.VelocityThrottleDuration
            ? throttling.VelocityWindow
            : throttling.VelocityThrottleDuration;
        var windowStart = now - effectiveWindow;
        var latestTimestamp = attemptInfo.RecentAttemptTimestamps.Max();

        var numberOfAttemptsInWindow = attemptInfo.RecentAttemptTimestamps.Count(attempt => attempt > windowStart);

        if (numberOfAttemptsInWindow >= throttling.MaxAttemptsPerWindow)
        {
            var velocityBlockedUntil = latestTimestamp + throttling.VelocityThrottleDuration;
            if (now < velocityBlockedUntil)
            {
                return new AuthenticationAttemptDecision.Reject();
            }
        }

        return new AuthenticationAttemptDecision.Allow();
    }
}
