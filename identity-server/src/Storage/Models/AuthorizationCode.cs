// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models an authorization code.
/// </summary>
public class AuthorizationCode
{
    /// <summary>
    /// Gets or sets the UTC time when this authorization code was created.
    /// </summary>
    /// <value>
    /// The creation time.
    /// </value>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of this authorization code in seconds.
    /// </summary>
    /// <value>
    /// The life time.
    /// </value>
    public int Lifetime { get; set; }

    /// <summary>
    /// Gets or sets the ID of the client that requested this authorization code.
    /// </summary>
    /// <value>
    /// The ID of the client.
    /// </value>
    public string ClientId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the authenticated subject (user) associated with this authorization code.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal Subject { get; set; } = default!;

    /// <summary>
    /// Gets or sets a value indicating whether this authorization code was issued as part of an OpenID Connect flow.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is an OpenID Connect flow; otherwise, <c>false</c>.
    /// </value>
    public bool IsOpenId { get; set; }

    /// <summary>
    /// Gets or sets the scopes that were requested when this authorization code was issued.
    /// </summary>
    /// <value>
    /// The requested scopes.
    /// </value>
    public IEnumerable<string> RequestedScopes { get; set; } = default!;

    /// <summary>
    /// Gets or sets the resource indicators that were requested when this authorization code was issued.
    /// </summary>
    public IEnumerable<string>? RequestedResourceIndicators { get; set; }

    /// <summary>
    /// Gets or sets the redirect URI that was used in the authorization request.
    /// The token endpoint will validate that the same redirect URI is presented when redeeming the code.
    /// </summary>
    /// <value>
    /// The redirect URI.
    /// </value>
    public string RedirectUri { get; set; } = default!;

    /// <summary>
    /// Gets or sets the nonce from the authorization request, used to prevent replay attacks in OpenID Connect flows.
    /// </summary>
    /// <value>
    /// The nonce.
    /// </value>
    public string? Nonce { get; set; }

    /// <summary>
    /// Gets or sets the hashed state value from the authorization request, used to produce the <c>s_hash</c> claim.
    /// </summary>
    /// <value>
    /// The hashed state.
    /// </value>
    public string? StateHash { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the consent screen was shown to the user during this authorization request.
    /// </summary>
    /// <value>
    ///   <c>true</c> if consent was shown; otherwise, <c>false</c>.
    /// </value>
    public bool WasConsentShown { get; set; }

    /// <summary>
    /// Gets or sets the session identifier associated with the user's authentication session at the time this code was issued.
    /// </summary>
    /// <value>
    /// The session identifier.
    /// </value>
    public string SessionId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the PKCE code challenge sent in the authorization request.
    /// Must be verified against the code verifier when the code is redeemed.
    /// </summary>
    /// <value>
    /// The code challenge.
    /// </value>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// Gets or sets the PKCE code challenge method (e.g. <c>S256</c> or <c>plain</c>).
    /// </summary>
    /// <value>
    /// The code challenge method
    /// </value>
    public string? CodeChallengeMethod { get; set; }

    /// <summary>
    /// Gets or sets the thumbprint of the DPoP proof key associated with this authorization code, if DPoP was used.
    /// </summary>
    public string? DPoPKeyThumbprint { get; set; }

    /// <summary>
    /// Gets or sets the description the user assigned to the device being authorized.
    /// </summary>
    /// <value>
    /// The description.
    /// </value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets additional properties associated with this authorization code.
    /// </summary>
    /// <value>
    /// The properties
    /// </value>
    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}
