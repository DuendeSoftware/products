// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Common;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Samlp;

/// <summary>
/// Abstract base class for requests
/// </summary>
public class RequestAbstractType
{
    /// <summary>
    /// Id of the request.
    /// </summary>
    public string Id { get; set; } = XmlHelpers.CreateId();

    /// <summary>
    /// Version of message, should always be 2.0
    /// </summary>
    public string Version { get; set; } = "2.0";

    /// <summary>
    /// Issue instant
    /// </summary>
    public DateTimeUtc IssueInstant { get; set; }

    /// <summary>
    /// Identifies the entity that generated the request message.
    /// </summary>
    public NameId? Issuer { get; set; }

    /// <summary>
    /// Destination Url that the messages is/was sent to.
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>
    /// URI reference for consent.
    /// </summary>
    public string? Consent { get; set; }

    /// <summary>
    /// Extensions
    /// </summary>
    public Common.Extensions? Extensions { get; set; }

    /// <summary>
    /// Trust level, based on signature validation
    /// </summary>
    public TrustLevel TrustLevel { get; set; }

    /// <summary>
    /// Whether the request was signed and the signature was verified against a configured key.
    /// This is a convenience property equivalent to <c>TrustLevel &gt;= TrustLevel.ConfiguredKey</c>.
    /// </summary>
    public bool HasTrustedSignature => TrustLevel >= TrustLevel.ConfiguredKey;
}
