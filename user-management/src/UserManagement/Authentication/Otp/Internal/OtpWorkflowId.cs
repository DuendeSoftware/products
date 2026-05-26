// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.UserManagement.Authentication.Otp.Internal;

internal readonly record struct OtpWorkflowId
{
    public OtpWorkflowId() => throw new InvalidOperationException();

    private OtpWorkflowId(UuidV7 uuid) => Uuid = uuid;

    internal UuidV7 Uuid { get; }

    public override string ToString() => Uuid.ToString();

    internal static OtpWorkflowId New() => new(UuidV7.New());

    internal static OtpWorkflowId Load(UuidV7 uuid) => new(uuid);
}
