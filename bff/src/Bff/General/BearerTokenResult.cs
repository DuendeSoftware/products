// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;


/// <summary>
/// Represents a bearer token result obtained during access token retrieval.
/// </summary>
public class BearerTokenResult(string accessToken) : AccessTokenResult
{
    /// <summary>
    /// The access token.
    /// </summary>
    public string AccessToken => accessToken;
}
