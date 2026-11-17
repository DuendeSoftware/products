// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;
using Duende.UserManagement.Authentication.External.Internal.Storage;
using Duende.UserManagement.Authentication.Otp.Internal.Storage;
using Duende.UserManagement.Authentication.Passkeys.Internal.Storage;
using Duende.UserManagement.Authentication.Totp.Internal.Storage;

namespace Duende.UserManagement.Authentication.Internal.Storage;

internal static class UserAuthenticatorsDso
{
    internal static readonly EntityType EntityType = new(1000, "UserAuthenticatorsDso");

    /// <summary>
    /// Converts a stored UserAuthenticatorsDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(UserAuthenticatorsDso)} type {dso.GetType()}.")
    };

    internal sealed record V1(
        Guid Id,
        string SubjectId,
        List<OtpAddressDso.V1> OtpAddresses,
        List<ExternalAuthenticatorAddressDso.V1> ExternalAuthenticatorAddresses,
        List<TotpDeviceDso.V1> TotpDevices,
        List<Pbkdf2HashedRecoveryCodeDso.V1> RecoveryCodes,
        HashedPasswordDso.V1? HashedPassword,
        List<PasskeyCredentialDso.V1> PasskeyCredentials,
        List<AuthenticatorFailureStateDso.V1>? FailureStates,
        List<HashedPasswordDso.V1>? PasswordHistory,
        DateTimeOffset? PasswordSetAtUtc) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}
