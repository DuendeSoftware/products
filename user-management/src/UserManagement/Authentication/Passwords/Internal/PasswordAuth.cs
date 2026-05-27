// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Licensing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Passwords.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class PasswordAuth(
    UserAuthenticatorsRepository repo,
    IAuthenticationAttemptPolicy attemptPolicy,
    ILogger<PasswordAuth> logger,
    IOptions<UserAuthenticationOptions> options,
    TimeProvider timeProvider,
    PasswordHashAlgorithms algorithms,
    UserManagementLicenseValidator licenseValidator) : IPasswordAuth
{
    public async Task<PasswordAuthenticationResult> TryAuthenticateAsync(UserName userName, NonValidatedPassword password, Ct ct)
    {
        licenseValidator.ValidatePassword();
        using var userNameScope = logger.BeginUserNameScope(userName);
        logger.PasswordAuthenticationStarted(LogLevel.Debug, userName);

        var record = await repo.TryReadAsync(userName, ct);
        var key = new AuthenticatorKey.Password();

        if (record is null)
        {
            logger.PasswordAuthenticationUserNotFound(LogLevel.Information, userName);
        }
        else
        {
            var attemptInfo = record.Value.UserAuthenticators.GetFailureState(key);
            var context = new AuthenticationAttemptContext(
                record.Value.UserAuthenticators.SubjectId,
                new AuthenticatorAttemptInfo(key, attemptInfo.FailedAttemptCount, attemptInfo.LastFailedAtUtc, attemptInfo.RecentAttemptTimestamps.AsReadOnly(), attemptInfo.LockoutCount));

            if (await attemptPolicy.EvaluateAsync(context, ct) is AuthenticationAttemptDecision.Reject)
            {
                logger.PasswordAuthenticationThrottled(LogLevel.Warning, userName);
                _ = Authentication.Internal.UserAuthenticators.TryAuthenticate(null, password, algorithms.Preferred, algorithms.All);
                return PasswordAuthenticationResult.Failure.Instance;
            }
        }

        var result = Authentication.Internal.UserAuthenticators.TryAuthenticate(record?.UserAuthenticators, password, algorithms.Preferred, algorithms.All);

        if (record is not null)
        {
            using var subjectIdScope = logger.BeginSubjectScope(record.Value.UserAuthenticators.SubjectId);
            var now = timeProvider.GetUtcNow();
            record.Value.UserAuthenticators.RecordAttempt(key, now, options.Value.Throttling.EffectiveVelocityRetentionWindow);

            if (result.Authenticated)
            {
                logger.PasswordAuthenticationSucceeded(LogLevel.Information, userName);
                record.Value.UserAuthenticators.ResetFailedAttempts(key);

                if (result.NeedsRehash)
                {
                    var previousAlgorithmId = record.Value.UserAuthenticators.HashedPassword?.AlgorithmId;
                    record.Value.UserAuthenticators.RehashPassword(password.Value, algorithms.Preferred);
                    logger.PasswordRehashed(LogLevel.Information, previousAlgorithmId, algorithms.Preferred.AlgorithmId);
                }
            }
            else
            {
                logger.PasswordAuthenticationFailed(LogLevel.Information, userName);
                record.Value.UserAuthenticators.RecordFailedAttempt(
                    key,
                    now,
                    options.Value.Throttling.FailureWindow,
                    options.Value.Throttling.MaxFailedAttempts);
            }
        }

        if (record is not null && await repo.UpdateAsync(record.Value.UserAuthenticators, record.Value.Version, ct) is not UpdateResult.Success)
        {
            if (!result.Authenticated)
            {
                logger.OptimisticConcurrencyRetry(LogLevel.Warning);

                await AuthenticationFailureRecorder.RetryFailedAttemptAsync(
                    userName,
                    key,
                    repo,
                    logger,
                    timeProvider,
                    options.Value.Throttling.FailureWindow,
                    options.Value.Throttling.EffectiveVelocityRetentionWindow,
                    options.Value.Throttling.MaxFailedAttempts,
                    ct);
            }

            return PasswordAuthenticationResult.Failure.Instance;
        }

        if (!result.Authenticated || record is null)
        {
            return PasswordAuthenticationResult.Failure.Instance;
        }

        return CheckExpiration(record.Value.UserAuthenticators, options.Value.Passwords.MaxAgeDays, timeProvider.GetUtcNow());
    }

    private PasswordAuthenticationResult CheckExpiration(
        Authentication.Internal.UserAuthenticators authenticators,
        int? maxAgeDays,
        DateTimeOffset now)
    {
        var subjectId = authenticators.SubjectId;

        if (maxAgeDays is null or <= 0)
        {
            return new PasswordAuthenticationResult.Success(subjectId);
        }

        var clampedMaxAge = Math.Min(maxAgeDays.Value, 36500);
        var passwordSetAt = authenticators.PasswordSetAtUtc;
        if (passwordSetAt is null)
        {
            PasswordAuthLogMessages.PasswordExpiredUnknownAge(logger, subjectId.Value);
            return new PasswordAuthenticationResult.Expired(subjectId);
        }

        if (now >= passwordSetAt.Value.AddDays(clampedMaxAge))
        {
            var days = (int)(now - passwordSetAt.Value).TotalDays;
            PasswordAuthLogMessages.PasswordExpired(logger, subjectId.Value, days);
            return new PasswordAuthenticationResult.Expired(subjectId);
        }

        return new PasswordAuthenticationResult.Success(subjectId);
    }
}

internal static partial class PasswordAuthLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Authentication attempt update hit an optimistic concurrency conflict for password authentication; retrying once to record the failed attempt.")]
    internal static partial void OptimisticConcurrencyRetry(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Password re-hashed from algorithm '{PreviousAlgorithmId}' to preferred algorithm '{NewAlgorithmId}'.")]
    internal static partial void PasswordRehashed(ILogger logger, string? previousAlgorithmId, string newAlgorithmId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Password for user '{SubjectId}' has expired after {Days} days.")]
    internal static partial void PasswordExpired(ILogger logger, string subjectId, int days);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Password for user '{SubjectId}' treated as expired because the password creation date is unknown.")]
    internal static partial void PasswordExpiredUnknownAge(ILogger logger, string subjectId);
}
