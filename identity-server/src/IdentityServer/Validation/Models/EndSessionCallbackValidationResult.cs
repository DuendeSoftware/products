// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validation result for end session callback requests.
/// </summary>
/// <seealso cref="ValidationResult" />
public class EndSessionCallbackValidationResult : ValidationResult
{
    /// <summary>
    /// Gets the client front-channel logout urls.
    /// </summary>
    public IEnumerable<string>? FrontChannelLogoutUrls { get; set; }

    /// <summary>
    /// Gets or sets the SAML front-channel logout requests.
    /// </summary>
    public IEnumerable<ISamlFrontChannelLogout> SamlFrontChannelLogouts { get; set; } = [];
}
