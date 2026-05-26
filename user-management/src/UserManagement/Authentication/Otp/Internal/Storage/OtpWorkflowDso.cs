// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.UserManagement.Authentication.Internal.Storage;

namespace Duende.UserManagement.Authentication.Otp.Internal.Storage;

internal static class OtpWorkflowDso
{
    internal static readonly EntityType EntityType = new(1001, "OtpWorkflowDso");

    internal sealed record V1(
        Guid Id,
        OtpAddressDso.V1 Address,
        Pbkdf2HashedPasswordDso.V1? HashedOtp,
        Guid? Token,
        DateTimeOffset? OtpExpiresAt,
        DateTimeOffset? OtpCreationBlockedUntil,
        List<DateTimeOffset> Attempts) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}
