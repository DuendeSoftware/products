// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;
using Duende.UserManagement.Authentication.Internal.Storage;

namespace Duende.UserManagement.Authentication.Otp.Internal.Storage;

internal static class OtpWorkflowDso
{
    internal static readonly EntityType EntityType = new(1001, "OtpWorkflowDso");

    /// <summary>
    /// Converts a stored OtpWorkflowDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(OtpWorkflowDso)} type {dso.GetType()}.")
    };

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
