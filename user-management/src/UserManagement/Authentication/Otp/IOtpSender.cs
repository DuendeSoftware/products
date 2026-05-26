// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

public interface IOtpSender
{
    bool CanSend(OtpAddress address);

    Task SendAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct);
}
