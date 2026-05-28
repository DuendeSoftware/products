// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Common;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Samlp;

/// <summary>
/// Saml2 p abstract StatusResponseType
/// </summary>
public class StatusResponseType
{
    /// <summary>
    /// Id of the request.
    /// </summary>
    public string Id { get; set; } = XmlHelpers.CreateId();

    /// <summary>
    /// Optional Id of the request message that this is response to.
    /// </summary>
    public string? InResponseTo { get; set; }

    /// <summary>
    /// Version of message, should always be 2.0
    /// </summary>
    public string Version { get; set; } = "2.0";

    /// <summary>
    /// Issue instant
    /// </summary>
    public DateTimeUtc IssueInstant { get; set; }

    /// <summary>
    /// Saml status
    /// </summary>
    public SamlStatus Status { get; set; } = default!;

    /// <summary>
    /// Destination of the message
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>
    /// Issuer of the message
    /// </summary>
    public NameId? Issuer { get; set; }

    /// <summary>
    /// Trust level, based on signature validation
    /// </summary>
    public TrustLevel TrustLevel { get; set; }

    /// <summary>
    /// Extensions
    /// </summary>
    public Common.Extensions? Extensions { get; set; }
}
