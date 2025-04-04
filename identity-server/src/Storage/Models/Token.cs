// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityModel;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models a token.
/// </summary>
public class Token
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> class.
    /// </summary>
    public Token()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> class.
    /// </summary>
    /// <param name="tokenType">Type of the token.</param>
    public Token(string tokenType) => Type = tokenType;

    /// <summary>
    /// A list of allowed algorithm for signing the token. If null or empty, will use the default algorithm.
    /// </summary>
    public ICollection<string> AllowedSigningAlgorithms { get; set; } = new HashSet<string>();

    /// <summary>
    /// Specifies the confirmation method of the token. This value, if set, will become the cnf claim.
    /// </summary>
    public string? Confirmation { get; set; }

    /// <summary>
    /// Gets or sets the audiences.
    /// </summary>
    /// <value>
    /// The audiences.
    /// </value>
    public ICollection<string> Audiences { get; set; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets the issuer.
    /// </summary>
    /// <value>
    /// The issuer.
    /// </value>
    public string Issuer { get; set; } = default!;

    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    /// <value>
    /// The creation time.
    /// </value>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the lifetime.
    /// </summary>
    /// <value>
    /// The lifetime.
    /// </value>
    public int Lifetime { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    /// <value>
    /// The type.
    /// </value>
    public string Type { get; set; } = OidcConstants.TokenTypes.AccessToken;

    /// <summary>
    /// Gets or sets the ID of the client.
    /// </summary>
    /// <value>
    /// The ID of the client.
    /// </value>
    public string ClientId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the type of access token of the client
    /// </summary>
    /// <value>
    /// The access token type specified by the client.
    /// </value>
    public AccessTokenType AccessTokenType { get; set; }

    /// <summary>
    /// Gets the description the user assigned to the device being authorized.
    /// </summary>
    /// <value>
    /// The description.
    /// </value>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates if the token should have a 'jti' claim value.
    /// </summary>
    public bool IncludeJwtId { get; set; }

    /// <summary>
    /// Gets or sets the claims.
    /// </summary>
    /// <value>
    /// The claims.
    /// </value>
    public ICollection<Claim> Claims { get; set; } = new HashSet<Claim>(new ClaimComparer());

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    /// <value>
    /// The version.
    /// </value>
    public int Version { get; set; } = 5;

    /// <summary>
    /// Gets the subject identifier.
    /// </summary>
    /// <value>
    /// The subject identifier.
    /// </value>
    public string? SubjectId => Claims.Where(x => x.Type == JwtClaimTypes.Subject).Select(x => x.Value).SingleOrDefault();

    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    /// <value>
    /// The session identifier.
    /// </value>
    public string? SessionId => Claims.Where(x => x.Type == JwtClaimTypes.SessionId).Select(x => x.Value).SingleOrDefault();

    /// <summary>
    /// Gets the scopes.
    /// </summary>
    /// <value>
    /// The scopes.
    /// </value>
    public IEnumerable<string> Scopes => Claims.Where(x => x.Type == JwtClaimTypes.Scope).Select(x => x.Value);
}
