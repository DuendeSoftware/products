// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.UserManagement.Authentication.Totp.Internal;

internal sealed record TotpDevice(TotpDeviceName Name, PlainBytesTotpKey Key, ulong LastSuccessfulTimeStep)
{
    internal static TotpDevice Load(TotpDeviceName name, PlainBytesTotpKey key, ulong lastSuccessfulTimeStep) =>
        new(name, key, lastSuccessfulTimeStep);
}
