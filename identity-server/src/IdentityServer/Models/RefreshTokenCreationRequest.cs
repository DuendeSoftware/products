// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models the data to create a refresh token from a validated request.
/// </summary>
public class RefreshTokenCreationRequest
{
    /// <summary>
    /// The client.
    /// </summary>
    public Client Client { get; set; } = default!;

    /// <summary>
    /// The subject.
    /// </summary>
    public ClaimsPrincipal Subject { get; set; } = default!;

    /// <summary>
    /// The description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The scopes.
    /// </summary>
    public IEnumerable<string> AuthorizedScopes { get; set; } = default!;

    /// <summary>
    /// The resource indicators. Null indicates there was no authorization step, thus no restrictions.
    /// Non-null means there was an authorization step, and subsequent requested resource indicators must be in the original list.
    /// </summary>
    public IEnumerable<string>? AuthorizedResourceIndicators { get; set; }

    /// <summary>
    /// The requested resource indicator.
    /// </summary>
    public string? RequestedResourceIndicator { get; set; }

    /// <summary>
    /// The access token.
    /// </summary>
    public Token AccessToken { get; set; } = default!;

    /// <summary>
    /// The proof type used.
    /// </summary>
    public ProofType ProofType { get; set; }

    /// <summary>
    /// Called to validate the <see cref="RefreshTokenCreationRequest"/> before it is processed.
    /// </summary>
#pragma warning disable CA1822 // Changing this on a public method in a public class would be a breaking change.
    public void Validate()
#pragma warning restore CA1822
    {
        //if (ValidatedResources == null) throw new ArgumentNullException(nameof(ValidatedResources));
        //if (ValidatedRequest == null) throw new ArgumentNullException(nameof(ValidatedRequest));
    }
}
