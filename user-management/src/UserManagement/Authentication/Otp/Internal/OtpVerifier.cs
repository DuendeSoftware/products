// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Otp.Internal.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Otp.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class OtpVerifier(
    OtpWorkflowRepository workflowRepository,
    ILogger<OtpVerifier> logger,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Verifies the OTP against the stored challenge and persists the attempt.
    /// Returns the verified address, or <c>null</c> if verification or persistence failed.
    /// </summary>
    internal async Task<OtpAddress?> TryVerifyAsync(PlainTextOtp otp, OtpToken token, Ct ct)
    {
        var workflowRecord = await workflowRepository.TryReadAsync(token, ct);

        var otpAddress = OtpWorkflow.TryVerify(workflowRecord?.OtpWorkflow, otp, timeProvider);

        if (workflowRecord is null)
        {
            logger.OtpVerificationWorkflowNotFound(LogLevel.Information);
            return null;
        }

        var expired = workflowRecord.Value.OtpWorkflow.OtpExpiresAt <= timeProvider.GetUtcNow();
        if (expired)
        {
            logger.OtpVerificationWorkflowExpired(LogLevel.Information, workflowRecord.Value.OtpWorkflow.Address);
        }

        if (await workflowRepository.UpdateAsync(workflowRecord.Value.OtpWorkflow, workflowRecord.Value.Version, ct)
                is not UpdateResult.Success)
        {
            logger.OtpVerificationUpdateFailed(LogLevel.Information);
            return null;
        }

        if (otpAddress is null && !expired)
        {
            logger.OtpVerificationFailed(LogLevel.Information);
        }

        return otpAddress;
    }
}
