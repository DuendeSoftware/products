// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.RecoveryCodes;

namespace Duende.UserManagement.Import;

/// <summary>
/// Authenticators to import for a user.
/// </summary>
public sealed record AuthenticatorImport
{
    /// <summary>OTP addresses (email, SMS, app) to import.</summary>
    public IReadOnlyCollection<OtpAddress>? OtpAddresses { get; init; }

    /// <summary>External authenticators (federated identity providers) to import.</summary>
    public IReadOnlyCollection<ExternalAuthenticatorAddress>? ExternalAuthenticatorAddresses { get; init; }

    /// <summary>Passkey credentials to import.</summary>
    public IReadOnlyCollection<PasskeyImport>? Passkeys { get; init; }

    /// <summary>Password to import.</summary>
    public PasswordImport? Password { get; init; }

    /// <summary>TOTP authenticators to import (e.g., migrated authenticator keys).</summary>
    public IReadOnlyCollection<TotpDeviceImport>? TotpDevices { get; init; }

    /// <summary>Recovery codes to import (plain text, will be hashed on import).</summary>
    public IReadOnlyCollection<PlainTextRecoveryCode>? RecoveryCodes { get; init; }
}
