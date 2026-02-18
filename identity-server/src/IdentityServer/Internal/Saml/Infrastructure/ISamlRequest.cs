// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

/// <summary>
/// Interface for SAML requests that have common validation fields
/// </summary>
internal interface ISamlRequest
{
    internal static abstract string MessageName { get; }
    internal string Issuer { get; }
    internal SamlVersion Version { get; }
    internal DateTime IssueInstant { get; }
    internal Uri? Destination { get; }
}
