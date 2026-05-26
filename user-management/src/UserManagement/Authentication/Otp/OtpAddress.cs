// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

public sealed record OtpAddress(OtpChannel Channel, ISubjectId SubjectId)
{
    internal static OtpAddress Load(OtpChannel channel, ISubjectId subjectId) => new(channel, subjectId);
}
