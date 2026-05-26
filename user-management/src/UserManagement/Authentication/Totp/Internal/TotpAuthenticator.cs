// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Totp.Internal;

internal sealed record TotpAuthenticator(TotpAuthenticatorName Name, PlainBytesTotpKey Key, ulong LastSuccessfulTimeStep)
{
    internal static TotpAuthenticator Load(TotpAuthenticatorName name, PlainBytesTotpKey key, ulong lastSuccessfulTimeStep) =>
        new(name, key, lastSuccessfulTimeStep);
}
