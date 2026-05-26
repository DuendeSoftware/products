// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Parsed authenticator data.
/// </summary>
internal sealed record AuthenticatorData(
    byte[] RpIdHash,
    AuthenticatorDataFlags Flags,
    uint SignCount,
    AttestedCredentialData? AttestedCredential)
{
    internal static bool TryParse(ReadOnlySpan<byte> authData, [NotNullWhen(true)] out AuthenticatorData? result)
    {
        result = null;

        if (authData.Length < WebAuthnConstants.AuthenticatorDataLayout.HeaderLength)
        {
            return false;
        }

        var rpIdHash = authData[..WebAuthnConstants.AuthenticatorDataLayout.RpIdHashLength].ToArray();
        var flags = (AuthenticatorDataFlags)authData[WebAuthnConstants.AuthenticatorDataLayout.RpIdHashLength];
        // https://www.w3.org/TR/webauthn-3/#sctn-authenticator-data
        // 32bit unsigned big endian integer
        var signCount =
            BinaryPrimitives.ReadUInt32BigEndian(
                authData.Slice(WebAuthnConstants.AuthenticatorDataLayout.RpIdHashLength +
                               WebAuthnConstants.AuthenticatorDataLayout.FlagsLength));

        AttestedCredentialData? attestedCredential = null;

        if (flags.HasFlag(AuthenticatorDataFlags.AttestedCredentialData))
        {
            if (!AttestedCredentialData.TryParse(authData[WebAuthnConstants.AuthenticatorDataLayout.HeaderLength..],
                    out attestedCredential))
            {
                return false;
            }
        }

        result = new AuthenticatorData(rpIdHash, flags, signCount, attestedCredential);
        return true;
    }
}
