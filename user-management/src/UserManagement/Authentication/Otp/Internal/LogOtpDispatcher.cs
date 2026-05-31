// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Otp.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
#pragma warning disable CA1848 // Use the LoggerMessage delegates
#pragma warning disable CA1873 // Avoid potentially expensive logging
// performance is irrelevant here, because a production app will not use this type
internal sealed class LogOtpDispatcher(ILogger<LogOtpDispatcher> logger) : IOtpDispatcher
{
    public bool CanDispatch(OtpAddress address) => true;

    public Task DispatchAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct)
    {
        var obj = new { Address = address, Otp = otp.Text, ExpiresAfter = expiresAfter };
        logger.LogInformation("One-time password dispatched: {OtpDispatched}", obj);
        return Task.CompletedTask;
    }
}
