// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Totp.Internal.Storage;

internal static class TotpDeviceDso
{
    internal sealed record V1(string Name, string Key, ulong LastSuccessfulTimeStep);
}
