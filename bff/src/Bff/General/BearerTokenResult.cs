// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;


/// <summary>
/// Represents a bearer token result obtained during access token retrieval.
/// </summary>
public class BearerTokenResult : AccessTokenResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BearerTokenResult"/> class
    /// with the specified access token.
    /// </summary>
    /// <param name="accessToken">The access token.</param>
    public BearerTokenResult(string accessToken) => AccessToken = accessToken;
    /// <summary>
    /// The access token.
    /// </summary>
    public string AccessToken { get; private set; }
}
