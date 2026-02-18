// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Internal.Saml;

internal readonly record struct SamlMessageName(string Value)
{
    public static readonly SamlMessageName SamlResponse = new("SAMLResponse");

    public static readonly SamlMessageName SamlRequest = new("SAMLRequest");

    public static implicit operator SamlMessageName(string value) => new(value);

    public override string ToString() => Value;
}
