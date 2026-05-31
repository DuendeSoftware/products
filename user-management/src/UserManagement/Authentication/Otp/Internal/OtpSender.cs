// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Otp.Internal.Storage;
using Duende.UserManagement.Internal.Licensing;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Otp.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class OtpSender(
    OtpWorkflowRepository workflowRepository,
    IEnumerable<IOtpDispatcher> otpDispatchers,
    ILogger<OtpSender> logger,
    TimeProvider timeProvider,
    UserManagementLicenseValidator licenseValidator) : IOtpSender
{
    public async Task<SendOtpResult?> TrySendOtpAsync(OtpAddress address, Ct ct)
    {
        licenseValidator.ValidateOtp();
        logger.OtpSendStarted(LogLevel.Debug, address);

        var record = await workflowRepository.TryReadAsync(address, ct);
        var workflow = record?.OtpWorkflow ?? new OtpWorkflow(address);

        var created = workflow.TryCreateOtp(
            timeProvider,
            out var otp,
            out var token,
            out var expiresAfter,
            out var expiresAtUtc,
            out var sendingBlockedFor,
            out var sendingBlockedUntilUtc);

        if (record is null)
        {
            if (await workflowRepository.CreateAsync(workflow, ct) is not CreateResult.Success)
            {
                logger.OtpWorkflowCreateFailed(LogLevel.Warning, address);
                return null;
            }
        }
        else
        {
            if (await workflowRepository.UpdateAsync(workflow, record.Value.Version, ct) is not UpdateResult.Success)
            {
                logger.OtpWorkflowUpdateFailed(LogLevel.Warning, address);
                return null;
            }
        }

        if (!created)
        {
            logger.OtpSendingBlocked(LogLevel.Information, address, sendingBlockedFor);
            return SendOtpResult.CreateNotSent(sendingBlockedFor, sendingBlockedUntilUtc);
        }

        if (otpDispatchers.LastOrDefault(d => d.CanDispatch(address)) is not { } dispatcher)
        {
            logger.OtpDispatcherNotRegistered(LogLevel.Error, address);
            throw new InvalidOperationException($"No {nameof(IOtpDispatcher)} found for {address}");
        }

        await dispatcher.DispatchAsync(address, otp!.Value, expiresAfter!.Value, ct);

        logger.OtpSent(LogLevel.Information, address);

        return SendOtpResult.CreateSent(
            token!, expiresAfter.Value, expiresAtUtc!.Value, sendingBlockedFor, sendingBlockedUntilUtc);
    }
}
