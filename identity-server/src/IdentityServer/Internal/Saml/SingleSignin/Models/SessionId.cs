// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

internal readonly record struct SessionId(Guid Value)
{
    public static SessionId NewId() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
