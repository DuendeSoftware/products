// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;


/// <summary>
/// Represents a DPoP token result obtained during access token retrieval.
/// </summary>
public class DPoPTokenResult : AccessTokenResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DPoPTokenResult"/> class
    /// with the specified access token and DPoP Json Web Key.
    /// </summary>
    /// <param name="accessToken">The access token.</param>
    /// <param name="dpopJWK">The DPoP Json Web Key.</param>
    public DPoPTokenResult(string accessToken, string dpopJWK)
    {
        AccessToken = accessToken;
        DPoPJsonWebKey = dpopJWK;
    }

    /// <summary>
    /// The access token.
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// The DPoP Json Web key
    /// </summary>
    public string DPoPJsonWebKey { get; set; }
}
