// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml.Linq;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

/// <summary>
/// Base record for SAML request wrappers that contain both the parsed request
/// and HTTP binding metadata.
/// </summary>
/// <typeparam name="TRequest">The type of the parsed SAML request</typeparam>
internal abstract record SamlRequestBase<TRequest> where TRequest : ISamlRequest
{
    public required TRequest Request { get; init; }

    public required XDocument RequestXml { get; init; }

    public required SamlBinding Binding { get; init; }

    public string? RelayState { get; init; }

    public string? Signature { get; init; }

    public string? SignatureAlgorithm { get; init; }

    public string? EncodedSamlRequest { get; init; }
}
