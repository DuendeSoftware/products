// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;


/// <summary>
/// Represents a DPoP token result obtained during access token retrieval.
/// </summary>
public class DPoPTokenResult(string accessToken, string dpopJWK) : AccessTokenResult
{
    /// <summary>
    /// The access token.
    /// </summary>
    public string AccessToken => accessToken;

    /// <summary>
    /// The DPoP Json Web key
    /// </summary>
    public string DPoPJsonWebKey => dpopJWK;
}
