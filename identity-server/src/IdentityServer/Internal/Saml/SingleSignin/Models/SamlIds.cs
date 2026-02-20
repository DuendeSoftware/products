// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Generates SAML-compliant ID values.
/// SAML 2.0 IDs must conform to xs:ID (NCName), which cannot start with a digit,
/// so we prefix with underscore as required by the spec.
/// </summary>
internal static class SamlIds
{
    internal static string NewRequestId() => "_" + Guid.NewGuid().ToString("N");
    internal static string NewResponseId() => "_" + Guid.NewGuid().ToString("N");
    internal static string NewAssertionId() => "_" + Guid.NewGuid().ToString("N");
}
