// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.UserManagement.Authentication.Internal.Storage;

namespace Duende.UserManagement.Authentication.Otp.Internal.Storage;

internal sealed record OtpAddressDskV1 : IDataStorageKey
{
    private OtpAddressDskV1(string channel, string value)
    {
        Channel = channel;
        Value = value;
    }

    public static DataStorageKeyVersion DskVersion { get; } =
        new(UserAuthenticatorsRepository.Keys.OtpAddress, 1);

    public string Channel { get; }
    public string Value { get; }

    public static OtpAddressDskV1 Create(OtpAddress otpAddress) =>
        new(otpAddress.Channel.Value, otpAddress.SubjectId.ToString());
}
