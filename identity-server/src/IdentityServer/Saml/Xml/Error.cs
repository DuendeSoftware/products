// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;

namespace Duende.IdentityServer.Saml.Xml;

/// <summary>
/// Represents an error that occured during parsing.
/// </summary>
/// <param name="Reason">Reason code.</param>
/// <param name="LocalName">The expected local name of the node/attribute</param>
/// <param name="Node">The XML Node that caused the error</param>
/// <param name="Message">Message explaining, contains technical details.</param>
public record Error(ErrorReason Reason, string? LocalName, XmlNode? Node, string Message)
{
    /// <summary>
    /// Do not throw for this error
    /// </summary>
    public bool Ignore { get; set; }

    /// <summary>
    /// The string value of the failed operation, if applicable.
    /// </summary>
    public string? StringValue { get; set; }
}
