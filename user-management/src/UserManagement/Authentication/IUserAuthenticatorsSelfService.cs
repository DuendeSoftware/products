// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.RecoveryCodes;
using Duende.UserManagement.Authentication.Totp;

namespace Duende.UserManagement.Authentication;

public interface IUserAuthenticatorsSelfService
{
    Task<PlainTextPassword> CreatePasswordAsync(UserSubjectId userId, string passwordString, Ct ct);

    Task<PasswordCreationResult> TryCreatePasswordAsync(UserSubjectId userId, string passwordString, Ct ct);

    Task<UserAuthenticators?> TryRegisterAsync(UserSubjectId subjectId, ExternalAuthenticator authenticator, Ct ct);

    public Task<UserAuthenticators?> TryGetAsync(UserSubjectId subjectId, Ct ct);

    public Task<UserAuthenticators?> TryGetAsync(ExternalAuthenticator authenticator, Ct ct);

    Task<bool> TryAddOtpAddressAsync(UserSubjectId subjectId, OtpAddress address, Ct ct);

    Task<bool> TryReplaceOtpAddressAsync(UserSubjectId subjectId, OtpAddress oldAddress, OtpAddress newAddress, Ct ct);

    Task<bool> TryRemoveOtpAddressAsync(UserSubjectId subjectId, OtpAddress address, Ct ct);

    Task<bool> TryAddExternalAuthenticatorAsync(UserSubjectId subjectId, ExternalAuthenticator authenticator, Ct ct);

    Task<bool> TryRemoveExternalAuthenticatorAsync(UserSubjectId subjectId, ExternalAuthenticator authenticator, Ct ct);

    Task<bool> TryAddTotpAuthenticatorAsync(UserSubjectId subjectId, TotpAuthenticatorName authenticatorName, PlainBytesTotpKey key, PlainTextTotp totp, Ct ct);

    Task<bool> TryRemoveTotpAuthenticatorAsync(UserSubjectId subjectId, TotpAuthenticatorName authenticatorName, Ct ct);

    Task<bool> TryAddPasskeyAsync(UserSubjectId subjectId, PasskeyCredentialData credential, Ct ct);

    Task<bool> TryRemovePasskeyAsync(UserSubjectId subjectId, PasskeyCredentialId credentialId, Ct ct);

    Task<IReadOnlyCollection<PlainTextRecoveryCode>?> TryCreateRecoveryCodesAsync(UserSubjectId subjectId, Ct ct);

    Task<bool> TrySetPasswordAsync(UserSubjectId subjectId, PlainTextPassword password, Ct ct);

    Task<bool> TryChangePasswordAsync(UserSubjectId subjectId, NonValidatedPassword oldPassword, PlainTextPassword newPassword, Ct ct);

    public Task<bool> TryResetPasswordAsync(UserSubjectId subjectId, PlainTextPassword password, Ct ct);
}
