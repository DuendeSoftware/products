// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.Passwords.Internal;
using Duende.UserManagement.Authentication.RecoveryCodes;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserAuthenticatorsSelfService(
    UserAuthenticatorsRepository repo,
    IAuthenticationAttemptPolicy authenticationAttemptPolicy,
    ILogger<UserAuthenticatorsSelfService> logger,
    IOptions<UserAuthenticationOptions> options,
    TimeProvider timeProvider,
    PasswordHashAlgorithms passwordHashAlgorithms,
    PlainTextPasswordFactory passwordFactory)
    : IUserAuthenticatorsSelfService
{
    public async Task<PlainTextPassword> CreatePasswordAsync(UserSubjectId userId, string passwordString, Ct ct)
    {
        var result = await passwordFactory.CreateAsync(userId, passwordString, ct);
        return result switch
        {
            PasswordCreationResult.Success success => success.Password,
            PasswordCreationResult.Failed failed => throw new FormatException(
                $"The value is not a valid {nameof(PlainTextPassword)}. {string.Join(" ", failed.Errors)}"),
            _ => throw new InvalidOperationException("Unexpected password creation result.")
        };
    }

    public Task<PasswordCreationResult> TryCreatePasswordAsync(UserSubjectId userId, string passwordString, Ct ct) =>
        passwordFactory.CreateAsync(userId, passwordString, ct);

    public async Task<Authentication.UserAuthenticators?> TryRegisterAsync(UserSubjectId subjectId, ExternalAuthenticator authenticator, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        var user = new UserAuthenticators(subjectId, [], [authenticator]);
        return await repo.CreateAsync(user, ct) is CreateResult.Success ? new Authentication.UserAuthenticators(user) : null;
    }

    public async Task<Authentication.UserAuthenticators?> TryGetAsync(UserSubjectId subjectId, Ct ct) =>
        await repo.TryReadAsync(subjectId, ct) is ({ } user, _) ? new Authentication.UserAuthenticators(user) : null;

    public async Task<Authentication.UserAuthenticators?> TryGetAsync(ExternalAuthenticator authenticator, Ct ct) =>
        await repo.TryReadAsync(authenticator, ct) is ({ } user, _) ? new Authentication.UserAuthenticators(user) : null;

    public async Task<bool> TryAddOtpAddressAsync(UserSubjectId subjectId, OtpAddress address, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        record.UserAuthenticators.Add([address]);
        var succeeded = await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success;
        if (succeeded)
        {
            logger.OtpAddressAdded(LogLevel.Information, subjectId);
        }

        return succeeded;
    }

    public async Task<bool> TryReplaceOtpAddressAsync(
        UserSubjectId subjectId, OtpAddress oldAddress, OtpAddress newAddress, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        record.UserAuthenticators.Remove([oldAddress]);
        record.UserAuthenticators.Add([newAddress]);
        var succeeded = await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success;
        if (succeeded)
        {
            logger.OtpAddressReplaced(LogLevel.Information, subjectId);
        }
        else
        {
            logger.OtpAddressReplaceFailed(LogLevel.Warning, subjectId);
        }

        return succeeded;
    }

    public async Task<bool> TryRemoveOtpAddressAsync(UserSubjectId subjectId, OtpAddress address, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        record.UserAuthenticators.Remove([address]);
        var succeeded = await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success;
        if (succeeded)
        {
            logger.OtpAddressRemoved(LogLevel.Information, subjectId);
        }

        return succeeded;
    }

    public async Task<bool> TryAddExternalAuthenticatorAsync(
        UserSubjectId subjectId, ExternalAuthenticator authenticator, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        record.UserAuthenticators.Add([authenticator]);
        var succeeded = await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success;
        if (succeeded)
        {
            logger.ExternalAuthenticatorAdded(LogLevel.Information, subjectId);
        }

        return succeeded;
    }

    public async Task<bool> TryRemoveExternalAuthenticatorAsync(
        UserSubjectId subjectId, ExternalAuthenticator authenticator, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        record.UserAuthenticators.Remove([authenticator]);
        var succeeded = await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success;
        if (succeeded)
        {
            logger.ExternalAuthenticatorRemoved(LogLevel.Information, subjectId);
        }

        return succeeded;
    }

    public async Task<bool> TryAddTotpAuthenticatorAsync(UserSubjectId subjectId, TotpAuthenticatorName authenticatorName, PlainBytesTotpKey key, PlainTextTotp totp, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        var added = record.UserAuthenticators.TryAdd(authenticatorName, key, totp, timeProvider);

        if (!added)
        {
            logger.TotpAddDuplicate(LogLevel.Information, subjectId);
        }

        return await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success && added;
    }

    public async Task<bool> TryRemoveTotpAuthenticatorAsync(UserSubjectId subjectId, TotpAuthenticatorName authenticatorName, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        record.UserAuthenticators.Remove([authenticatorName]);

        return await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success;
    }

    public async Task<bool> TryAddPasskeyAsync(UserSubjectId subjectId, PasskeyCredentialData credential, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        var passkeyCredential = PasskeyCredential.Create(
            timeProvider,
            credential.CredentialId,
            [.. credential.PublicKeyCose],
            credential.Algorithm,
            credential.SignCount,
            credential.BackupEligible,
            credential.BackedUp,
            credential.Aaguid,
            credential.Name);

        var added = record.UserAuthenticators.TryAdd(passkeyCredential);
        if (!added)
        {
            logger.PasskeyAddDuplicate(LogLevel.Information, subjectId);
            return false;
        }

        return await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success;
    }

    public async Task<bool> TryRemovePasskeyAsync(UserSubjectId subjectId, PasskeyCredentialId credentialId, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        var isRemoved = record.UserAuthenticators.TryRemove(credentialId);
        if (!isRemoved)
        {
            logger.PasskeyRemoveNotFound(LogLevel.Information, subjectId);
            return false;
        }

        return await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success;
    }

    public async Task<IReadOnlyCollection<PlainTextRecoveryCode>?> TryCreateRecoveryCodesAsync(UserSubjectId subjectId, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (!options.Value.RecoveryCodes.Enabled)
        {
            return null;
        }

        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return null;
        }

        var codes = record.UserAuthenticators.CreateRecoveryCodes(options.Value.RecoveryCodes.Count);

        var succeeded = await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success;
        if (succeeded)
        {
            logger.RecoveryCodesCreated(LogLevel.Information, subjectId);
        }
        else
        {
            logger.RecoveryCodesCreateFailed(LogLevel.Warning, subjectId);
        }

        return succeeded ? codes : null;
    }

    public async Task<bool> TrySetPasswordAsync(UserSubjectId subjectId, PlainTextPassword password, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        var isSet = record.UserAuthenticators.TrySetPassword(password, passwordHashAlgorithms.Preferred, timeProvider);
        var succeeded = await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success && isSet;
        if (succeeded)
        {
            logger.PasswordSetSucceeded(LogLevel.Information, subjectId);
        }
        else
        {
            logger.PasswordSetFailed(LogLevel.Warning, subjectId);
        }

        return succeeded;
    }

    public async Task<bool> TryChangePasswordAsync(
        UserSubjectId subjectId, NonValidatedPassword oldPassword, PlainTextPassword newPassword, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        var key = new Authentication.AuthenticatorKey.Password();
        var attemptInfo = record.UserAuthenticators.GetFailureState(key);
        var context = new AuthenticationAttemptContext(
            record.UserAuthenticators.SubjectId,
            new AuthenticatorAttemptInfo(key, attemptInfo.FailedAttemptCount, attemptInfo.LastFailedAtUtc, attemptInfo.RecentAttemptTimestamps.AsReadOnly(), attemptInfo.LockoutCount));

        if (await authenticationAttemptPolicy.EvaluateAsync(context, ct) is AuthenticationAttemptDecision.Reject)
        {
            _ = UserAuthenticators.TryAuthenticate(null, oldPassword, passwordHashAlgorithms.Preferred, passwordHashAlgorithms.All);
            logger.PasswordChangeThrottled(LogLevel.Warning, subjectId);
            return false;
        }

        var isChanged = record.UserAuthenticators.TryChangePassword(oldPassword, newPassword, passwordHashAlgorithms.Preferred, passwordHashAlgorithms.All, options.Value.Passwords.HistoryCount, timeProvider);

        var now = timeProvider.GetUtcNow();
        record.UserAuthenticators.RecordAttempt(key, now, options.Value.Throttling.EffectiveVelocityRetentionWindow);

        if (isChanged)
        {
            record.UserAuthenticators.ResetFailedAttempts(key);
        }
        else
        {
            logger.PasswordChangeOldPasswordIncorrect(LogLevel.Information, subjectId);
            record.UserAuthenticators.RecordFailedAttempt(
                key,
                now,
                options.Value.Throttling.FailureWindow,
                options.Value.Throttling.MaxFailedAttempts);
        }

        if (await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is not UpdateResult.Success)
        {
            if (!isChanged)
            {
                logger.PasswordChangeOptimisticConcurrencyRetry(LogLevel.Information);

                await AuthenticationFailureRecorder.RetryFailedAttemptAsync(
                    subjectId,
                    key,
                    repo,
                    logger,
                    timeProvider,
                    options.Value.Throttling.FailureWindow,
                    options.Value.Throttling.EffectiveVelocityRetentionWindow,
                    options.Value.Throttling.MaxFailedAttempts,
                    ct);
            }

            return false;
        }

        if (isChanged)
        {
            logger.PasswordChangeSucceeded(LogLevel.Information, subjectId);
        }
        else
        {
            logger.PasswordChangeFailed(LogLevel.Warning, subjectId);
        }

        return isChanged;
    }

    public async Task<bool> TryResetPasswordAsync(UserSubjectId subjectId, PlainTextPassword password, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not { } record)
        {
            logger.UserNotFound(LogLevel.Warning, subjectId);
            return false;
        }

        var isReset = record.UserAuthenticators.TryResetPassword(password, passwordHashAlgorithms.Preferred, passwordHashAlgorithms.All, options.Value.Passwords.HistoryCount, timeProvider);
        var succeeded = await repo.UpdateAsync(record.UserAuthenticators, record.Version, ct) is UpdateResult.Success && isReset;
        if (succeeded)
        {
            logger.PasswordResetSucceeded(LogLevel.Information, subjectId);
        }
        else
        {
            logger.PasswordResetFailed(LogLevel.Warning, subjectId);
        }

        return succeeded;
    }
}

internal static class AuthenticationFailureRecorder
{
    internal static async Task RetryFailedAttemptAsync(
        UserSubjectId subjectId,
        AuthenticatorKey key,
        UserAuthenticatorsRepository repo,
        ILogger logger,
        TimeProvider timeProvider,
        TimeSpan failureWindow,
        TimeSpan velocityWindow,
        int maxFailedAttempts,
        Ct ct)
    {
        // We intentionally retry exactly once here. Without a retry, two simultaneous requests can
        // undercount a failed attempt due to optimistic concurrency. Retrying many times improves
        // accuracy under contention, but also makes timing behavior more variable. The chosen trade-off
        // is a single retry: enough to handle the normal two-request race, while expecting higher-level
        // protections to absorb true flood scenarios.
        if (await repo.TryReadAsync(subjectId, ct) is not { } retryRecord)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        retryRecord.UserAuthenticators.RecordAttempt(key, now, velocityWindow);
        retryRecord.UserAuthenticators.RecordFailedAttempt(key, now, failureWindow, maxFailedAttempts);
        if (await repo.UpdateAsync(retryRecord.UserAuthenticators, retryRecord.Version, ct) is not UpdateResult.Success)
        {
            logger.FailedAttemptRetryOptimisticConcurrencyConflict(LogLevel.Error);
        }
    }

    internal static async Task RetryFailedAttemptAsync(
        UserName userName,
        AuthenticatorKey key,
        UserAuthenticatorsRepository repo,
        ILogger logger,
        TimeProvider timeProvider,
        TimeSpan failureWindow,
        TimeSpan velocityWindow,
        int maxFailedAttempts,
        Ct ct)
    {
        // We intentionally retry exactly once here. Without a retry, two simultaneous requests can
        // undercount a failed attempt due to optimistic concurrency. Retrying many times improves
        // accuracy under contention, but also makes timing behavior more variable. The chosen trade-off
        // is a single retry: enough to handle the normal two-request race, while expecting higher-level
        // protections to absorb true flood scenarios.
        if (await repo.TryReadAsync(userName, ct) is not { } retryRecord)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        retryRecord.UserAuthenticators.RecordAttempt(key, now, velocityWindow);
        retryRecord.UserAuthenticators.RecordFailedAttempt(key, now, failureWindow, maxFailedAttempts);
        if (await repo.UpdateAsync(retryRecord.UserAuthenticators, retryRecord.Version, ct) is not UpdateResult.Success)
        {
            logger.FailedAttemptRetryOptimisticConcurrencyConflict(LogLevel.Error);
        }
    }
}
