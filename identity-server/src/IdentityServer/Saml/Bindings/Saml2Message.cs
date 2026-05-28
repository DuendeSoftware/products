// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace Duende.IdentityServer.Saml.Bindings;

using Saml;

/// <summary>
/// Represents a Saml2 message as seen by the binding.
/// </summary>
public abstract class Saml2Message
{
    /// <summary>
    /// Name of the message to be used in query strings, form fields etc.
    /// This is typically "SamlRequest" or "SamlResponse".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// RelayState included with the message
    /// </summary>
    public string? RelayState { get; init; }

    /// <summary>
    /// The XML payload.
    /// </summary>
    public required XmlElement Xml { get; init; }

    /// <summary>
    /// Destination URL of the message. For outbound messages the URL
    /// to send the message to. For inbound, the URL the message was
    /// received at.
    /// </summary>
    public required string Destination { get; init; }

    /// <summary>
    /// Binding to use when sending the message, or used when message was read.
    /// </summary>
    public required string Binding { get; init; }
}

/// <summary>
/// An outbound Saml2 message to be sent via a binding.
/// </summary>
/// <remarks>
/// This class is not sealed because <see cref="HttpPostBinding"/> has a
/// <c>protected virtual void SignMessage(OutboundSaml2Message)</c> method
/// that subclasses may need to override.
/// </remarks>
public class OutboundSaml2Message : Saml2Message
{
    /// <summary>
    /// Signing certificate that the message should be signed with. The
    /// method for signing is binding dependent.
    /// </summary>
    public X509Certificate2? SigningCertificate { get; init; }
}

/// <summary>
/// An inbound Saml2 message received via a binding.
/// </summary>
public sealed class InboundSaml2Message : Saml2Message
{
    /// <summary>
    /// Trust level of the message, based on binding-level signature validation.
    /// For HTTP-Redirect binding, this is set to <see cref="TrustLevel.ConfiguredKey"/>
    /// when the query string signature is validated against SP signing certificates.
    /// For HTTP-POST binding, this remains <see cref="TrustLevel.None"/> as
    /// signature validation is performed during XML parsing.
    /// </summary>
    public TrustLevel TrustLevel { get; init; }

}
