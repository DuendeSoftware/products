// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// Abstraction for dispatching OTP codes via a specific channel (e.g., email, SMS).
/// Implement this interface to support custom OTP delivery mechanisms.
/// </summary>
public interface IOtpDispatcher
{
    /// <summary>
    /// Returns <c>true</c> if this dispatcher can deliver to the specified OTP address.
    /// </summary>
    /// <param name="address">The OTP address to check.</param>
    bool CanDispatch(OtpAddress address);

    /// <summary>
    /// Dispatches the OTP code to the specified address.
    /// </summary>
    /// <param name="address">The OTP address to deliver to.</param>
    /// <param name="otp">The plain text OTP code to dispatch.</param>
    /// <param name="expiresAfter">How long the OTP is valid.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DispatchAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct);
}
