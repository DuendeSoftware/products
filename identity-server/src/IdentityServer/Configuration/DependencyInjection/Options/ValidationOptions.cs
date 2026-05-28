// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings that control redirect URI validation behavior for the authorize and end-session
/// endpoints.
/// </summary>
public class ValidationOptions
{
    /// <summary>
    /// Gets URI scheme prefixes that are never accepted as custom URI schemes in the
    /// <c>redirect_uri</c> parameter of the authorize endpoint or the
    /// <c>post_logout_redirect_uri</c> parameter of the end-session endpoint.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>javascript:</c>, <c>file:</c>, <c>data:</c>, <c>mailto:</c>,
    /// <c>ftp:</c>, <c>blob:</c>, <c>about:</c>, <c>ssh:</c>, <c>tel:</c>,
    /// <c>view-source:</c>, <c>ws:</c>, and <c>wss:</c>. These schemes are blocked because
    /// they can be exploited for open redirect or cross-site scripting attacks.
    /// </remarks>
    public ICollection<string> InvalidRedirectUriPrefixes { get; } = new HashSet<string>
    {
        "javascript:",
        "file:",
        "data:",
        "mailto:",
        "ftp:",
        "blob:",
        "about:",
        "ssh:",
        "tel:",
        "view-source:",
        "ws:",
        "wss:"
    };
}
