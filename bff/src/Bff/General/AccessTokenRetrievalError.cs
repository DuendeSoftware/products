// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// Represents an error that occurred during the retrieval of an access token.
/// </summary>
public class AccessTokenRetrievalError(string error) : AccessTokenResult
{
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Error => error;
}

public class NoAccessTokenReturnedError(string error) : AccessTokenRetrievalError(error);
public class MissingDPopTokenError(string error) : AccessTokenRetrievalError(error);
public class UnexpectedAccessTokenError(string error) : AccessTokenRetrievalError(error);
