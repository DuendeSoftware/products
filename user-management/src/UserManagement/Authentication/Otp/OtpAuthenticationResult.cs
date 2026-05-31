// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// Represents the result of an OTP authentication attempt.
/// </summary>
public abstract record OtpAuthenticationResult
{
    private OtpAuthenticationResult() { }

    /// <summary>
    /// Indicates a successful OTP authentication.
    /// </summary>
    public sealed record Success : OtpAuthenticationResult
    {
        internal Success(OtpAddress address, UserSubjectId userSubjectId)
        {
            Address = address;
            UserSubjectId = userSubjectId;
        }

        /// <summary>Gets the OTP address that was authenticated.</summary>
        public OtpAddress Address { get; }

        /// <summary>Gets the subject ID of the authenticated user.</summary>
        public UserSubjectId UserSubjectId { get; }
    }

    /// <summary>
    /// Indicates a failed OTP authentication attempt.
    /// </summary>
    public sealed record Failure : OtpAuthenticationResult
    {
        internal static Failure Instance { get; } = new();

        private Failure() { }
    }
}
