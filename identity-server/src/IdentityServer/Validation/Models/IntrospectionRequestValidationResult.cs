// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validation result for introspection request
/// </summary>
/// <seealso cref="ValidationResult" />
public class IntrospectionRequestValidationResult : ValidationResult
{
    /// <summary>
    /// Gets or sets the request parameters.
    /// </summary>
    /// <value>
    /// The parameters.
    /// </value>
    public NameValueCollection Parameters { get; set; }

    /// <summary>
    /// Gets or sets the API.
    /// </summary>
    /// <value>
    /// The API.
    /// </value>
    public ApiResource Api { get; set; }

    /// <summary>
    /// Gets or sets the client.
    /// </summary>
    /// <value>
    /// The client.
    /// </value>
    public Client Client { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the token is active.
    /// </summary>
    /// <value>
    ///   <c>true</c> if the token is active; otherwise, <c>false</c>.
    /// </value>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the claims.
    /// </summary>
    /// <value>
    /// The claims.
    /// </value>
    public IEnumerable<Claim> Claims { get; set; }

    /// <summary>
    /// Gets or sets the token.
    /// </summary>
    /// <value>
    /// The token.
    /// </value>
    public string Token { get; set; }
}

/// <summary>
/// Failure reasons for introspection request
/// </summary>
public enum IntrospectionRequestValidationFailureReason
{
    /// <summary>
    /// none
    /// </summary>
    None,

    /// <summary>
    /// missing token
    /// </summary>
    MissingToken,

    /// <summary>
    /// invalid token
    /// </summary>
    InvalidToken,

    /// <summary>
    /// invalid scope
    /// </summary>
    InvalidScope
}