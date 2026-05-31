// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// Provides OTP-based authentication: verifying an OTP code and resolving or creating the associated user.
/// </summary>
public interface IOtpAuthenticator
{
    /// <summary>
    /// Verifies an OTP code against the stored token for the given address.
    /// </summary>
    /// <param name="otp">The plain text OTP code submitted by the user.</param>
    /// <param name="token">The token identifying the OTP challenge session.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OtpAuthenticationResult> TryAuthenticateAsync(PlainTextOtp otp, OtpToken token, Ct ct);
}
