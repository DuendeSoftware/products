// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Totp;

namespace Duende.UserManagement.Import;

/// <summary>
/// A TOTP authenticator to import for a user.
/// </summary>
/// <param name="Name">The authenticator name (e.g., "Default").</param>
/// <param name="Key">The TOTP secret key in plain bytes.</param>
public sealed record TotpImport(TotpAuthenticatorName Name, PlainBytesTotpKey Key);
