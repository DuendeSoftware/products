// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Xml;

/// <summary>
/// Exception type thrown for Xml-related errors from the Saml2 library.
/// </summary>
public class SamlXmlException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SamlXmlException"/> class.
    /// </summary>
    public SamlXmlException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamlXmlException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message</param>
    public SamlXmlException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamlXmlException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    public SamlXmlException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamlXmlException"/> class from a collection of errors.
    /// </summary>
    /// <param name="errors">Error list</param>
    public SamlXmlException(IEnumerable<Error> errors)
        : base(string.Join(" ", errors.Where(e => !e.Ignore).Select(e => e.Message))) =>
        Errors = errors;

    /// <summary>
    /// Errors encountered.
    /// </summary>
    public IEnumerable<Error> Errors { get; } = [];
}
