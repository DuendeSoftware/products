// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Licensing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.RecoveryCodes.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class RecoveryCodeAuth(
    UserAuthenticatorsRepository repo,
    IAuthenticationAttemptPolicy attemptPolicy,
    ILogger<RecoveryCodeAuth> logger,
    IOptions<UserAuthenticationOptions> options,
    TimeProvider timeProvider,
    UserManagementLicenseValidator licenseValidator) : IRecoveryCodeAuth
{
    public async Task<bool> TryAuthenticateAsync(UserSubjectId subjectId, PlainTextRecoveryCode recoveryCode, Ct ct)
    {
        licenseValidator.ValidateRecoveryCode();
        using var scope = logger.BeginSubjectScope(subjectId);
        logger.RecoveryCodeAuthenticationStarted(LogLevel.Debug, subjectId);

        if (!options.Value.RecoveryCodes.Enabled)
        {
            return false;
        }

        var record = await repo.TryReadAsync(subjectId, ct);
        var key = new AuthenticatorKey.RecoveryCode();

        if (record is null)
        {
            logger.RecoveryCodeAuthenticationUserNotFound(LogLevel.Information, subjectId);
        }
        else
        {
            var attemptInfo = record.Value.UserAuthenticators.GetFailureState(key);
            var context = new AuthenticationAttemptContext(
                record.Value.UserAuthenticators.SubjectId,
                new AuthenticatorAttemptInfo(key, attemptInfo.FailedAttemptCount, attemptInfo.LastFailedAtUtc, attemptInfo.RecentAttemptTimestamps.AsReadOnly(), attemptInfo.LockoutCount));

            if (await attemptPolicy.EvaluateAsync(context, ct) is AuthenticationAttemptDecision.Reject)
            {
                logger.RecoveryCodeAuthenticationThrottled(LogLevel.Warning, subjectId);
                _ = Authentication.Internal.UserAuthenticators.TryAuthenticate(null, recoveryCode, options.Value.RecoveryCodes.Count);
                return false;
            }
        }

        var authenticated = Authentication.Internal.UserAuthenticators.TryAuthenticate(record?.UserAuthenticators, recoveryCode, options.Value.RecoveryCodes.Count);

        if (record is not null)
        {
            var now = timeProvider.GetUtcNow();
            record.Value.UserAuthenticators.RecordAttempt(key, now, options.Value.Throttling.EffectiveVelocityRetentionWindow);

            if (authenticated)
            {
                logger.RecoveryCodeAuthenticationSucceeded(LogLevel.Information, subjectId);
                record.Value.UserAuthenticators.ResetFailedAttempts(key);
            }
            else
            {
                logger.RecoveryCodeAuthenticationFailed(LogLevel.Information, subjectId);
                record.Value.UserAuthenticators.RecordFailedAttempt(
                    key,
                    now,
                    options.Value.Throttling.FailureWindow,
                    options.Value.Throttling.MaxFailedAttempts);
            }
        }

        if (record is not null && await repo.UpdateAsync(record.Value.UserAuthenticators, record.Value.Version, ct) is not UpdateResult.Success)
        {
            if (!authenticated)
            {
                logger.OptimisticConcurrencyRetry(LogLevel.Warning);

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

        return authenticated;
    }
}
