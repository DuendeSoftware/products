// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Context information for validating a user during backchannel authentication request.
/// </summary>
public class BackchannelAuthenticationUserValidatorContext
{
    /// <summary>
    /// Gets or sets the client.
    /// </summary>
    public Client Client { get; set; } = null!;

    /// <summary>
    /// Gets or sets the login hint token.
    /// </summary>
    public string? LoginHintToken { get; set; }

    /// <summary>
    /// Gets or sets the id token hint.
    /// </summary>
    public string? IdTokenHint { get; set; }

    /// <summary>
    /// Gets or sets the validated claims from the id token hint.
    /// </summary>
    public IEnumerable<Claim>? IdTokenHintClaims { get; set; }

    /// <summary>
    /// Gets or sets the login hint.
    /// </summary>
    public string? LoginHint { get; set; }

    /// <summary>
    /// Gets or sets the user code.
    /// </summary>
    public string? UserCode { get; set; }

    /// <summary>
    /// Gets or sets the binding message.
    /// </summary>
    public string? BindingMessage { get; set; }
}
