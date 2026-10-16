// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserAuthenticationOptionsValidator(
    SignatureVerifiers signatureVerifiers,
    IEnumerable<IAttestationFormatValidator> attestationFormatValidators)
    : IValidateOptions<UserAuthenticationOptions>
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

        // Passkey options
        var passkeys = options.Passkeys;
        if (passkeys.ChallengeSize < WebAuthnConstants.MinimumChallengeSize)
        {
            failures.Add($"Passkeys.ChallengeSize must be >= {WebAuthnConstants.MinimumChallengeSize}, but was {passkeys.ChallengeSize}.");
        }

        foreach (var group in signatureVerifiers.All.GroupBy(verifier => verifier.Algorithm).Where(group => group.Count() > 1))
        {
            failures.Add($"Multiple passkey signature verifiers are registered for COSE algorithm '{group.Key}'.");
        }

        var attestationFormats = new HashSet<string>(StringComparer.Ordinal);
        foreach (var validator in attestationFormatValidators)
        {
            var format = validator.Format;
            if (string.IsNullOrWhiteSpace(format))
            {
                failures.Add($"Passkey attestation validator '{validator.GetType().FullName}' must have a non-empty Format.");
            }
            else if (!attestationFormats.Add(format))
            {
                failures.Add($"Multiple passkey attestation validators are registered for format '{format}'.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
