// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Context for backchannel authentication request id validation.
/// </summary>
public class BackchannelAuthenticationRequestIdValidationContext
{
    /// <summary>
    /// Gets or sets the authentication request id.
    /// </summary>
    /// <value>
    /// The device code.
    /// </value>
    public string AuthenticationRequestId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    /// <value>
    /// The request.
    /// </value>
    public ValidatedTokenRequest Request { get; set; } = null!;

    /// <summary>
    /// Gets or sets the result.
    /// </summary>
    /// <value>
    /// The result.
    /// </value>
    public TokenRequestValidationResult? Result { get; set; }
}
