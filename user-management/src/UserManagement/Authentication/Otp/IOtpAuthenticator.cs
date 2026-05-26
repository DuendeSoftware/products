// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

public interface IOtpAuthenticator
{
    Task<SendOtpResult?> TrySendOtpAsync(OtpAddress address, Ct ct);

    Task<OtpAuthenticationResult> TryAuthenticateAsync(PlainTextOtp otp, OtpToken token, Ct ct);
}
