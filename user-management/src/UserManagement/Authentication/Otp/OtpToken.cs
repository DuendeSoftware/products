// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// An opaque token that identifies an active OTP session, returned when an OTP is sent
/// and required to verify the code during authentication.
/// </summary>
[ValueOf<Guid>]
public partial record OtpToken
{
    internal static OtpToken New() => new(UuidV7.New().Value);

    internal static bool TryValidate(Guid? input, out IReadOnlyList<string>? errors)
    {
        if (!UuidV7.TryValidate(input, out var uuid))
        {
            errors = new[] { "Otp token must be a UuidV7" };
            return false;
        }

        errors = null;
        return true;
    }

}
