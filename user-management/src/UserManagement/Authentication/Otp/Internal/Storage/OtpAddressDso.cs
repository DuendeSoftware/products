// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal.Storage;

namespace Duende.UserManagement.Authentication.Otp.Internal.Storage;

internal static class OtpAddressDso
{
    internal sealed record V1(string Channel, SubjectIdDso.V1 SubjectId);
}
