// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Authentication.Otp.Internal.Storage;

internal sealed record OtpWorkflowAddressDskV1 : IDataStorageKey
{
    private OtpWorkflowAddressDskV1(string channel, string subjectId)
    {
        Channel = channel;
        SubjectId = subjectId;
    }

    public static DataStorageKeyVersion DskVersion { get; } = new(OtpWorkflowRepository.Keys.Address, 1);

    public string Channel { get; }
    public string SubjectId { get; }

    // TODO: Canonicalize subjectId?
    public static OtpWorkflowAddressDskV1 Create(OtpAddress address) =>
        new(address.Channel.Value, address.SubjectId.ToString());
}
