// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models usage of a bearer token
/// </summary>
public class BearerTokenUsageValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the token was found.
    /// </summary>
    /// <value>
    ///   <c>true</c> if the token was found; otherwise, <c>false</c>.
    /// </value>
    public bool TokenFound { get; set; }

    /// <summary>
    /// Gets or sets the token.
    /// </summary>
    /// <value>
    /// The token.
    /// </value>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the usage type.
    /// </summary>
    /// <value>
    /// The type of the usage.
    /// </value>
    public BearerTokenUsageType UsageType { get; set; }
}