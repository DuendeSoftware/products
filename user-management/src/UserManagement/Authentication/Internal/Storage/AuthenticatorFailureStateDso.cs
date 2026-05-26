// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal.Storage;

internal static class AuthenticatorFailureStateDso
{
    internal sealed record V1(
        string AuthenticatorType,
        string? AuthenticatorId,
        int FailedAttemptCount,
        DateTimeOffset? LastFailedAtUtc,
        List<DateTimeOffset>? RecentAttemptTimestamps,
        int LockoutCount = 0);
}
