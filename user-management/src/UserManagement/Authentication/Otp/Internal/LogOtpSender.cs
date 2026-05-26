// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Otp.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA1848 // Use the LoggerMessage delegates
#pragma warning disable CA1873 // Avoid potentially expensive logging
// performance is irrelevant here, because a production app will not use this type
internal sealed class LogOtpSender(ILogger<LogOtpSender> logger) : IOtpSender
{
    public bool CanSend(OtpAddress address) => true;

    public Task SendAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct)
    {
        var obj = new { Address = address, Otp = otp.Text, ExpiresAfter = expiresAfter };
        logger.LogInformation("One-time password sent: {OtpSent}", obj);
        return Task.CompletedTask;
    }
}
