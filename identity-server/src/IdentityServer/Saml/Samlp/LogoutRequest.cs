// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Common;

namespace Duende.IdentityServer.Saml.Samlp;

/// <summary>
/// A SAML2p LogoutRequest
/// </summary>
public class LogoutRequest : RequestAbstractType
{
    /// <summary>
    /// The identifier and associated attributes that specify the principal as
    /// currently recognized by the identity and service providers prior to this request.
    /// </summary>
    public NameId? NameId { get; set; }

    /// <summary>
    /// The identifier that indexes this session at the message recipient.
    /// </summary>
    public string? SessionIndex { get; set; }

    /// <summary>
    /// A URI reference indicating the reason for the logout.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// The time at which the request expires, after which the recipient may discard the message.
    /// </summary>
    public DateTimeUtc? NotOnOrAfter { get; set; }
}
