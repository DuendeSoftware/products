// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Unique identifier for this assertion
/// Must start with a _ character and be unique
///
/// According to SAML 2.0 Core Specification (Section 1.3.4):
///- ID attributes must be of type xs:ID
///- xs:ID must conform to the NCName production (Non-Colonized Name) from the XML Namespaces specification
///- NCName cannot start with a digit, colon, or certain other characters
/// </summary>
internal readonly record struct RequestId(string Value)
{
    public static RequestId New() => new("_" + Guid.NewGuid().ToString("N"));

    public static implicit operator RequestId(string value) => new(value);

    public override string ToString() => Value;
}
