// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Totp;

public sealed class TotpOptions
{
    public StorageOptions Storage { get; } = new();
}
