// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.UserManagement.Authentication.External.Internal.Storage;
using Duende.UserManagement.Authentication.Otp.Internal.Storage;
using Duende.UserManagement.Authentication.Passkeys.Internal.Storage;
using Duende.UserManagement.Authentication.Totp.Internal.Storage;

namespace Duende.UserManagement.Authentication.Internal.Storage;

internal static class UserAuthenticatorsDso
{
    internal static readonly EntityType EntityType = new(1000, "UserAuthenticatorsDso");

    internal sealed record V1(
        Guid Id,
        string SubjectId,
        List<OtpAddressDso.V1> OtpAddresses,
        List<ExternalAuthenticatorDso.V1> ExternalAuthenticators,
        List<TotpAuthenticatorDso.V1> TotpAuthenticators,
        List<Pbkdf2HashedPasswordDso.V1> RecoveryCodes,
        HashedPasswordDso.V1? HashedPassword,
        List<PasskeyCredentialDso.V1> PasskeyCredentials,
        List<AuthenticatorFailureStateDso.V1>? FailureStates,
        List<HashedPasswordDso.V1>? PasswordHistory,
        DateTimeOffset? PasswordSetAtUtc) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}
