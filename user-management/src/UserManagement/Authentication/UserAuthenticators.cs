// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Totp;

namespace Duende.UserManagement.Authentication;

/// <summary>
/// A read-only snapshot of all authenticators registered for a user, including OTP addresses,
/// external authenticators, TOTP devices, passkeys, recovery codes, and password status.
/// </summary>
public sealed record UserAuthenticators
{
    internal UserAuthenticators(Internal.UserAuthenticators authenticators)
    {
        SubjectId = authenticators.SubjectId;
        OtpAddresses = authenticators.OtpAddresses;
        ExternalAuthenticators = authenticators.ExternalAuthenticators;
        TotpAuthenticatorNames = [.. authenticators.TotpAuthenticators.Keys];
        Passkeys = authenticators.PasskeyCredentials.Values
            .Select(c => new UserPasskey(c.CredentialId, c.Name, c.CreatedAt))
            .ToList();
        RecoveryCodeCount = authenticators.RecoveryCodes.Count;
        HasPassword = authenticators.HashedPassword is not null;
    }

    /// <summary>Gets the subject ID of the user.</summary>
    public UserSubjectId SubjectId { get; }
    /// <summary>Gets the OTP addresses registered for the user.</summary>
    public IReadOnlyCollection<OtpAddress> OtpAddresses { get; }
    /// <summary>Gets the external authenticators registered for the user.</summary>
    public IReadOnlyCollection<ExternalAuthenticator> ExternalAuthenticators { get; }
    /// <summary>Gets the names of TOTP authenticators registered for the user.</summary>
    public IReadOnlyCollection<TotpAuthenticatorName> TotpAuthenticatorNames { get; }
    /// <summary>Gets the passkeys registered for the user.</summary>
    public IReadOnlyCollection<UserPasskey> Passkeys { get; }
    /// <summary>Gets the number of remaining recovery codes for the user.</summary>
    public int RecoveryCodeCount { get; }
    /// <summary>Gets a value indicating whether the user has a password set.</summary>
    public bool HasPassword { get; }
}
