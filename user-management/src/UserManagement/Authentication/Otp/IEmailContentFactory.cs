// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// Factory for creating the email content used when sending OTP codes.
/// Implement this interface to customize the OTP email subject and body.
/// </summary>
public interface IEmailContentFactory
{
    /// <summary>
    /// Creates the email content for the given OTP code.
    /// </summary>
    /// <param name="otp">The plain text OTP code to include in the email.</param>
    /// <param name="expiresAfter">How long the OTP is valid.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<EmailContent> CreateAsync(PlainTextOtp otp, TimeSpan expiresAfter, Ct ct);
}
