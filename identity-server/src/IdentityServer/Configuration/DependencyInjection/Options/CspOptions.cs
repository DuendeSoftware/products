// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for Content Security Policy (CSP) headers emitted by IdentityServer on its
/// interactive pages.
/// </summary>
public class CspOptions
{
    /// <summary>
    /// Gets or sets the CSP specification level used when generating Content Security Policy headers.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="CspLevel.Two"/>. Set to <see cref="CspLevel.One"/> to accommodate
    /// older browsers that do not support CSP Level 2 directives.
    /// </remarks>
    public CspLevel Level { get; set; } = CspLevel.Two;

    /// <summary>
    /// Gets or sets a value indicating whether the legacy <c>X-Content-Security-Policy</c> header is emitted in
    /// addition to the standards-based <c>Content-Security-Policy</c> header.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. The <c>X-Content-Security-Policy</c> header was used by older
    /// browsers before the <c>Content-Security-Policy</c> header was standardized. Disable this
    /// if you do not need to support those legacy browsers.
    /// </remarks>
    public bool AddDeprecatedHeader { get; set; } = true;
}
