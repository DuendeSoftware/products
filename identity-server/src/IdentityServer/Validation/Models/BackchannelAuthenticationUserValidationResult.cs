// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Represents the result of a backchannel authentication request.
/// </summary>
public class BackchannelAuthenticationUserValidationResult
{
    /// <summary>
    /// Indicates if this represents an error.
    /// </summary>
    public bool IsError => !string.IsNullOrWhiteSpace(Error);

    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the error description.
    /// </summary>
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// Gets or sets the subject based upon the provided hint.
    /// </summary>
    public ClaimsPrincipal? Subject { get; set; }
}
