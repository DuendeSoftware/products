// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityModel;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models a refresh token.
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// Gets or sets the UTC time when this refresh token was created.
    /// </summary>
    /// <value>
    /// The creation time.
    /// </value>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of the refresh token in seconds.
    /// </summary>
    /// <value>
    /// The life time.
    /// </value>
    public int Lifetime { get; set; }

    /// <summary>
    /// Gets or sets the UTC time when this refresh token was consumed (redeemed for a new token).
    /// <c>null</c> means the token has not been consumed yet.
    /// </summary>
    /// <value>
    /// The consumed time.
    /// </value>
    public DateTime? ConsumedTime { get; set; }

    /// <summary>
    /// Obsolete. This property remains to keep backwards compatibility with serialized persisted grants.
    /// </summary>
    /// <value>
    /// The access token.
    /// </value>
    [Obsolete("Use AccessTokens or Set/GetAccessToken instead.")]
    public Token? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the resource indicator specific access tokens associated with this refresh token.
    /// Keyed by resource indicator (empty string for the default resource).
    /// </summary>
    /// <value>
    /// The access token.
    /// </value>
    public Dictionary<string, Token> AccessTokens { get; set; } = new Dictionary<string, Token>();

    /// <summary>
    /// Returns the access token based on the resource indicator.
    /// </summary>
    /// <param name="resourceIndicator"></param>
    /// <returns></returns>
    public Token? GetAccessToken(string? resourceIndicator = null)
    {
        AccessTokens.TryGetValue(resourceIndicator ?? string.Empty, out var token);
        return token;
    }

    /// <summary>
    /// Sets the access token based on the resource indicator.
    /// </summary>
    /// <param name="resourceIndicator"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public void SetAccessToken(Token token, string? resourceIndicator = null) => AccessTokens[resourceIndicator ?? string.Empty] = token;

    /// <summary>
    /// Gets or sets the original subject that requested the token.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal Subject { get; set; } = default!;

    /// <summary>
    /// Gets or sets the version number of the refresh token format. Used for backwards compatibility when deserializing persisted grants.
    /// </summary>
    /// <value>
    /// The version.
    /// </value>
    public int Version { get; set; } = 5;

    /// <summary>
    /// Gets or sets the client identifier.
    /// </summary>
    /// <value>
    /// The client identifier.
    /// </value>
    public string ClientId { get; set; } = default!;

    /// <summary>
    /// Gets the subject identifier.
    /// </summary>
    /// <value>
    /// The subject identifier.
    /// </value>
    public string? SubjectId => Subject?.FindFirst(JwtClaimTypes.Subject)?.Value;

    /// <summary>
    /// Gets or sets the session identifier.
    /// </summary>
    /// <value>
    /// The session identifier.
    /// </value>
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the description the user assigned to the device being authorized.
    /// </summary>
    /// <value>
    /// The description.
    /// </value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the scopes that were authorized when this refresh token was issued.
    /// </summary>
    /// <value>
    /// The scopes.
    /// </value>
    public IEnumerable<string> AuthorizedScopes { get; set; } = default!;

    /// <summary>
    /// Gets or sets the resource indicators authorized for this refresh token.
    /// <c>null</c> indicates there was no authorization step with resource indicators, so there are no restrictions.
    /// A non-null value means subsequent requested resource indicators must be a subset of this list.
    /// </summary>
    public IEnumerable<string>? AuthorizedResourceIndicators { get; set; }

    /// <summary>
    /// Gets or sets the type of proof used to bind this refresh token (e.g. DPoP). <c>null</c> indicates refresh tokens
    /// created prior to this property being added, or that no proof binding is used.
    /// </summary>
    public ProofType? ProofType { get; set; }
}
