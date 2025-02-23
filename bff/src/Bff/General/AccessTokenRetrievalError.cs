// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// Represents an error that occurred during the retrieval of an access token.
/// </summary>
public class AccessTokenRetrievalError : AccessTokenResult
{
    /// <summary>
    /// Initializes a new instance of the <see
    /// cref="AccessTokenRetrievalError"/> class with the specified error
    /// message.
    /// </summary>
    /// <param name="error">The error message.</param>
    public AccessTokenRetrievalError(string error)
    {
        Error = error;
    }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Error { get; set; }
}