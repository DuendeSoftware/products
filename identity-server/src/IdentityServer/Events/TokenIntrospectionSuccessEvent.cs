// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for successful token introspection
/// </summary>
/// <seealso cref="Event" />
public class TokenIntrospectionSuccessEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenIntrospectionSuccessEvent" /> class.
    /// </summary>
    /// <param name="result">The result.</param>
    public TokenIntrospectionSuccessEvent(IntrospectionRequestValidationResult result)
        : base(EventCategories.Token,
            "Token Introspection Success",
            EventTypes.Success,
            EventIds.TokenIntrospectionSuccess)
    {
        ApiName = result.Api?.Name;
        ClientName = result.Client?.ClientName;
        IsActive = result.IsActive;

        if (result.Token.IsPresent())
        {
            Token = Obfuscate(result.Token);
        }

        if (!IEnumerableExtensions.IsNullOrEmpty(result.Claims))
        {
            ClaimTypes = result.Claims.Select(c => c.Type).Distinct();
            TokenScopes = result.Claims.Where(c => c.Type == "scope").Select(c => c.Value);
        }
    }

    /// <summary>
    /// Gets or sets the name of the API.
    /// </summary>
    /// <value>
    /// The name of the API.
    /// </value>
    public string ApiName { get; set; }

    /// <summary>
    /// Gets or sets the name of the client.
    /// </summary>
    /// <value>
    /// The name of the client.
    /// </value>
    public string ClientName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is active.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is active; otherwise, <c>false</c>.
    /// </value>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the token.
    /// </summary>
    /// <value>
    /// The token.
    /// </value>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the claim types.
    /// </summary>
    /// <value>
    /// The claim types.
    /// </value>
    public IEnumerable<string> ClaimTypes { get; set; }

    /// <summary>
    /// Gets or sets the token scopes.
    /// </summary>
    /// <value>
    /// The token scopes.
    /// </value>
    public IEnumerable<string> TokenScopes { get; set; }
}
