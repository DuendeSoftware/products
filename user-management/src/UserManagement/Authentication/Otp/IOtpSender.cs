// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// Creates and sends OTPs to addresses for verification purposes.
/// </summary>
public interface IOtpSender
{
    /// <summary>
    /// Creates an OTP and sends it to the specified address.
    /// Returns <c>null</c> if the OTP could not be persisted.
    /// Throws if no <see cref="IOtpDispatcher"/> is registered for the address.
    /// </summary>
    /// <param name="address">The OTP address to send to.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SendOtpResult?> TrySendOtpAsync(OtpAddress address, Ct ct);
}
