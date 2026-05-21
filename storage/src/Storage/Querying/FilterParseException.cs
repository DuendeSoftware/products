// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Querying;

/// <summary>
/// Exception thrown when a filter expression cannot be parsed.
/// </summary>
public sealed class FilterParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="FilterParseException"/>.
    /// </summary>
    private FilterParseException() { }

    /// <summary>
    /// Initializes a new instance of <see cref="FilterParseException"/> with a message.
    /// </summary>
    public FilterParseException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of <see cref="FilterParseException"/> with a message and inner exception.
    /// </summary>
    public FilterParseException(string message, Exception innerException)
        : base(message, innerException) { }
}
