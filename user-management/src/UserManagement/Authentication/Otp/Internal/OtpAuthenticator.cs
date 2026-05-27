// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Authentication.Otp.Internal.Storage;
using Duende.UserManagement.Internal.Licensing;
using Microsoft.Extensions.Logging;
using InternalUserAuthenticators = Duende.UserManagement.Authentication.Internal.UserAuthenticators;

namespace Duende.UserManagement.Authentication.Otp.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class OtpAuthenticator(
    OtpWorkflowRepository workflowRepository,
    UserAuthenticatorsRepository authenticatorsRepository,
    IEnumerable<IOtpSender> otpSenders,
    ILogger<OtpAuthenticator> logger,
    TimeProvider timeProvider,
    UserManagementLicenseValidator licenseValidator) : IOtpAuthenticator
{
    public async Task<SendOtpResult?> TrySendOtpAsync(OtpAddress address, Ct ct)
    {
        licenseValidator.ValidateOtp();
        logger.OtpSendStarted(LogLevel.Debug, address);

        var record = await workflowRepository.TryReadAsync(address, ct);
        var authenticator = record?.OtpWorkflow ?? new OtpWorkflow(address);

        var created = authenticator.TryCreateOtp(
            timeProvider,
            out var otp,
            out var token,
            out var expiresAfter,
            out var expiresAtUtc,
            out var creationBlockedFor,
            out var creationBlockedUntilUtc);

        if (record is null)
        {
            if (await workflowRepository.CreateAsync(authenticator, ct) is not CreateResult.Success)
            {
                logger.OtpWorkflowCreateFailed(LogLevel.Warning, address);
                return null;
            }
        }
        else
        {
            if (await workflowRepository.UpdateAsync(authenticator, record.Value.Version, ct) is not UpdateResult.Success)
            {
                logger.OtpWorkflowUpdateFailed(LogLevel.Warning, address);
                return null;
            }
        }

        if (!created)
        {
            logger.OtpCreationBlocked(LogLevel.Information, address, creationBlockedFor);
            return SendOtpResult.CreateNotSent(creationBlockedFor, creationBlockedUntilUtc);
        }

        if (otpSenders.LastOrDefault(sender => sender.CanSend(address)) is not { } otpSender)
        {
            logger.OtpSenderNotRegistered(LogLevel.Error, address);
            throw new InvalidOperationException($"No {nameof(IOtpSender)} found for {address}");
        }

        await otpSender.SendAsync(address, otp!.Value, expiresAfter!.Value, ct);

        logger.OtpSent(LogLevel.Information, address);

        return SendOtpResult.CreateSent(
            token!.Value, expiresAfter.Value, expiresAtUtc!.Value, creationBlockedFor, creationBlockedUntilUtc);
    }

    public async Task<OtpAuthenticationResult> TryAuthenticateAsync(PlainTextOtp otp, OtpToken token, Ct ct)
    {
        licenseValidator.ValidateOtp();
        logger.OtpAuthenticationStarted(LogLevel.Debug);

        var workflowRecord = await workflowRepository.TryReadAsync(token, ct);

        var otpAddress = OtpWorkflow.TryAuthenticate(workflowRecord?.OtpWorkflow, otp, timeProvider);

        if (workflowRecord is null)
        {
            logger.OtpAuthenticationWorkflowNotFound(LogLevel.Information);
            return OtpAuthenticationResult.Failure.Instance;
        }

        var expired = workflowRecord.Value.OtpWorkflow.OtpExpiresAt <= timeProvider.GetUtcNow();
        if (expired)
        {
            logger.OtpWorkflowExpired(LogLevel.Information, workflowRecord.Value.OtpWorkflow.Address);
        }

        if (await workflowRepository.UpdateAsync(workflowRecord.Value.OtpWorkflow, workflowRecord.Value.Version, ct)
                is not UpdateResult.Success ||
            otpAddress is null)
        {
            if (otpAddress is null && !expired)
            {
                logger.OtpAuthenticationFailed(LogLevel.Information);
            }
            else if (otpAddress is not null)
            {
                logger.OtpAuthenticationUpdateFailed(LogLevel.Information);
            }

            return OtpAuthenticationResult.Failure.Instance;
        }

        logger.OtpAuthenticationSucceeded(LogLevel.Information, otpAddress);

        UserSubjectId subjectId;
        if (await authenticatorsRepository.TryReadAsync(otpAddress, ct) is { } authenticatorsRecord)
        {
            subjectId = authenticatorsRecord.UserAuthenticators.SubjectId;
        }
        else
        {
            var authenticators = new InternalUserAuthenticators(UserSubjectId.New(), [otpAddress], []);
            if (await authenticatorsRepository.CreateAsync(authenticators, ct) is not CreateResult.Success)
            {
                return OtpAuthenticationResult.Failure.Instance;
            }

            subjectId = authenticators.SubjectId;
        }

        return new OtpAuthenticationResult.Success(otpAddress, subjectId);
    }
}
