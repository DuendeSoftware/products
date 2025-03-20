// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models the data to create a token from a validated request.
/// </summary>
public class TokenCreationRequest
{
    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal? Subject { get; set; }

    /// <summary>
    /// Gets or sets the validated resources.
    /// </summary>
    /// <value>
    /// The resources.
    /// </value>
    public ResourceValidationResult ValidatedResources { get; set; } = null!;

    /// <summary>
    /// Gets or sets the validated request.
    /// </summary>
    /// <value>
    /// The validated request.
    /// </value>
    public ValidatedRequest ValidatedRequest { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether [include all identity claims].
    /// </summary>
    /// <value>
    /// <c>true</c> if [include all identity claims]; otherwise, <c>false</c>.
    /// </value>
    public bool IncludeAllIdentityClaims { get; set; }

    /// <summary>
    /// Gets or sets the access token to hash.
    /// </summary>
    /// <value>
    /// The access token to hash.
    /// </value>
    public string? AccessTokenToHash { get; set; }

    /// <summary>
    /// Gets or sets the authorization code to hash.
    /// </summary>
    /// <value>
    /// The authorization code to hash.
    /// </value>
    public string? AuthorizationCodeToHash { get; set; }

    /// <summary>
    /// Gets or sets pre-hashed state
    /// </summary>
    /// <value>
    /// The pre-hashed state
    /// </value>
    public string? StateHash { get; set; }

    /// <summary>
    /// Gets or sets the nonce.
    /// </summary>
    /// <value>
    /// The nonce.
    /// </value>
    public string? Nonce { get; set; }

    /// <summary>
    /// Gets the description the user assigned to the device being authorized.
    /// </summary>
    /// <value>
    /// The description.
    /// </value>
    public string? Description { get; set; }

    /// <summary>
    /// Called to validate the <see cref="TokenCreationRequest"/> before it is processed.
    /// </summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(ValidatedResources);
        ArgumentNullException.ThrowIfNull(ValidatedRequest);
    }
}
