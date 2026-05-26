// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Totp;

namespace Duende.UserManagement.Authentication;

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
        UserName = authenticators.UserName;
    }

    public UserSubjectId SubjectId { get; }
    public IReadOnlyCollection<OtpAddress> OtpAddresses { get; }
    public IReadOnlyCollection<ExternalAuthenticator> ExternalAuthenticators { get; }
    public IReadOnlyCollection<TotpAuthenticatorName> TotpAuthenticatorNames { get; }
    public IReadOnlyCollection<UserPasskey> Passkeys { get; }
    public int RecoveryCodeCount { get; }
    public bool HasPassword { get; }
    public UserName? UserName { get; }
}
