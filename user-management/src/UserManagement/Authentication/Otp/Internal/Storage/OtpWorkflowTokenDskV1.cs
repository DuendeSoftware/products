// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Authentication.Otp.Internal.Storage;

internal sealed record OtpWorkflowTokenDskV1 : IGuidDataStorageKey
{
    private OtpWorkflowTokenDskV1(Guid value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } = new(OtpWorkflowRepository.Keys.Token, 1);

    public Guid Value { get; }

    public static OtpWorkflowTokenDskV1 Create(OtpToken token) => new(token.Value);
}
