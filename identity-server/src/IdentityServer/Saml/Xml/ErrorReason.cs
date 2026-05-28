// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Xml;

/// <summary>
/// Error reasons in the error reporting.
/// </summary>
public enum ErrorReason
{
    /// <summary>
    /// The local of the node name was not the expected.
    /// </summary>
    UnexpectedLocalName = 1,

    /// <summary>
    /// The namespace of the node was not the expected.
    /// </summary>
    UnexpectedNamespace = 2,

    /// <summary>
    /// A required attribute was missing.
    /// </summary>
    MissingAttribute = 3,

    /// <summary>
    /// Value conversion failed for the attribute. The string
    /// representation is stored as <see cref="Error.StringValue"/>.
    /// </summary>
    ConversionFailed = 4,

    /// <summary>
    /// A string value that should be an absolute Uri wasn't that.
    /// </summary>
    NotAbsoluteUri = 5,

    /// <summary>
    /// When traversing child elements, an unsupported node type was encountered.
    /// </summary>
    UnsupportedNodeType = 6,

    /// <summary>
    /// Tried to move to next child element, but there was none as it should be.
    /// </summary>
    MissingElement = 7,

    /// <summary>
    /// A signature failed validation.
    /// </summary>
    SignatureFailure = 8,

    /// <summary>
    /// There are extra elements that were neither processed nor ignored.
    /// </summary>
    ExtraElements = 9,

    /// <summary>
    /// The element is present, but contains nothing.
    /// </summary>
    EmptyElement = 10,

    /// <summary>
    /// An element is recognized but not supported by this implementation.
    /// </summary>
    UnsupportedElement = 11,

    /// <summary>
    /// The combination of child elements is not valid per the schema.
    /// </summary>
    InvalidElementCombination = 12,
}
