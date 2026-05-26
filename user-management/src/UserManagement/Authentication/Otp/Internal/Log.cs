// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Otp.Internal;

internal static partial class Log
{
    [LoggerMessage(Message = $"Email sent to {{{Parameters.To}}}.")]
    internal static partial void EmailSent(this ILogger logger, LogLevel level, EmailAddress to);

    [LoggerMessage(Message = $"Failed to send email to {{{Parameters.To}}}.")]
    internal static partial void FailedToSendEmail(this ILogger logger, LogLevel level, Exception ex, EmailAddress to);

    [LoggerMessage(Message = $"OTP send attempt started for address {{{Parameters.OtpAddress}}}.")]
    internal static partial void OtpSendStarted(this ILogger logger, LogLevel level, OtpAddress otpAddress);

    [LoggerMessage(Message = $"OTP creation blocked for address {{{Parameters.OtpAddress}}}: rate limit active, blocked for {{{Parameters.BlockedFor}}}.")]
    internal static partial void OtpCreationBlocked(this ILogger logger, LogLevel level, OtpAddress otpAddress, TimeSpan blockedFor);

    [LoggerMessage(Message = $"OTP sent successfully to address {{{Parameters.OtpAddress}}}.")]
    internal static partial void OtpSent(this ILogger logger, LogLevel level, OtpAddress otpAddress);

    [LoggerMessage(Message = $"OTP workflow repository create failed for address {{{Parameters.OtpAddress}}}.")]
    internal static partial void OtpWorkflowCreateFailed(this ILogger logger, LogLevel level, OtpAddress otpAddress);

    [LoggerMessage(Message = $"OTP workflow repository update failed for address {{{Parameters.OtpAddress}}}.")]
    internal static partial void OtpWorkflowUpdateFailed(this ILogger logger, LogLevel level, OtpAddress otpAddress);

    [LoggerMessage(Message = $"No OTP sender registered for address {{{Parameters.OtpAddress}}}.")]
    internal static partial void OtpSenderNotRegistered(this ILogger logger, LogLevel level, OtpAddress otpAddress);

    [LoggerMessage(Message = "OTP authentication attempt started.")]
    internal static partial void OtpAuthenticationStarted(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = $"No {{{Parameters.SenderType}}} found for address {{{Parameters.OtpAddress}}}.")]
    internal static partial void NoOtpSenderFound(this ILogger logger, LogLevel level, string senderType, OtpAddress otpAddress);

    [LoggerMessage(Message = "OTP authentication failed: workflow not found for token.")]
    internal static partial void OtpAuthenticationWorkflowNotFound(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = $"OTP authentication failed: workflow expired for address {{{Parameters.OtpAddress}}}.")]
    internal static partial void OtpWorkflowExpired(this ILogger logger, LogLevel level, OtpAddress otpAddress);

    [LoggerMessage(Message = "OTP authentication failed: workflow update failed.")]
    internal static partial void OtpAuthenticationUpdateFailed(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = "OTP authentication failed: OTP verification failed.")]
    internal static partial void OtpAuthenticationFailed(this ILogger logger, LogLevel level);

    [LoggerMessage(Message = $"OTP authentication succeeded for address {{{Parameters.OtpAddress}}}.")]
    internal static partial void OtpAuthenticationSucceeded(this ILogger logger, LogLevel level, OtpAddress otpAddress);

    private static class Parameters
    {
        internal const string To = nameof(To);
        internal const string OtpAddress = nameof(OtpAddress);
        internal const string BlockedFor = nameof(BlockedFor);
        internal const string SenderType = nameof(SenderType);
    }
}
