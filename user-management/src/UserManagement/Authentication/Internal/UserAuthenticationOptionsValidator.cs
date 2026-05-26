// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserAuthenticationOptionsValidator : IValidateOptions<UserAuthenticationOptions>
{
    public ValidateOptionsResult Validate(string? name, UserAuthenticationOptions options)
    {
        var failures = new List<string>();

        // Password options
        var passwords = options.Passwords;
        if (passwords.HistoryCount < 0)
        {
            failures.Add($"Passwords.HistoryCount must be >= 0, but was {passwords.HistoryCount}.");
        }

        if (passwords.MaxAgeDays is <= 0)
        {
            failures.Add($"Passwords.MaxAgeDays must be > 0 when set, but was {passwords.MaxAgeDays}.");
        }

        if (passwords.MaxAgeDays is > 36500)
        {
            failures.Add($"Passwords.MaxAgeDays must be <= 36500 (100 years), but was {passwords.MaxAgeDays}.");
        }

        // Recovery code options
        var recoveryCodes = options.RecoveryCodes;
        if (recoveryCodes.Count is < 1 or > 50)
        {
            failures.Add($"RecoveryCodes.Count must be between 1 and 50, but was {recoveryCodes.Count}.");
        }

        // Throttling options
        var throttling = options.Throttling;
        if (throttling.MaxFailedAttempts < 1)
        {
            failures.Add($"Throttling.MaxFailedAttempts must be >= 1, but was {throttling.MaxFailedAttempts}.");
        }

        if (throttling.ThrottleDuration <= TimeSpan.Zero)
        {
            failures.Add($"Throttling.ThrottleDuration must be positive, but was {throttling.ThrottleDuration}.");
        }

        if (throttling.EscalatingThrottleDurations is { } durations)
        {
            for (var i = 0; i < durations.Count; i++)
            {
                if (durations[i] <= TimeSpan.Zero)
                {
                    failures.Add($"Throttling.EscalatingThrottleDurations[{i}] must be positive, but was {durations[i]}.");
                }
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
