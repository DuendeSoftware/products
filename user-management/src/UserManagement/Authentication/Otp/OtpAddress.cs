// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// Represents a delivery address for OTP codes, combining a channel (e.g., email, SMS) with a subject identifier.
/// </summary>
/// <param name="Channel">The delivery channel for the OTP (e.g., <see cref="OtpChannel.Email"/>).</param>
/// <param name="SubjectId">The channel-specific address identifier (e.g., an email address or phone number).</param>
public sealed record OtpAddress(OtpChannel Channel, ISubjectId SubjectId)
{
    internal static OtpAddress Load(OtpChannel channel, ISubjectId subjectId) => new(channel, subjectId);
}
