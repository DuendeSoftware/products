// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// The ValidationOptions contains settings that affect some of the default validation behavior.
/// </summary>
public class ValidationOptions
{
    /// <summary>
    ///  Collection of URI scheme prefixes that should never be used as custom
    ///  URI schemes in the redirect_uri passed to the authorize endpoint or the
    ///  post_logout_redirect_uri passed to the end_session endpoint.
    /// </summary>
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
