// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// Constants for Duende.BFF
/// </summary>
public static class Constants
{
    internal const string BffMiddlewareMarker = "Duende.Bff.BffMiddlewareMarker";

    /// <summary>
    /// Constants used for YARP
    /// </summary>
    public static class Yarp
    {
        /// <summary>
        /// Name of token type (User, Client, UserOrClient) metadata
        /// </summary>
        public const string TokenTypeMetadata = "Duende.Bff.Yarp.TokenType";

        /// <summary>
        /// Name of Anti-forgery check metadata
        /// </summary>
        public const string AntiforgeryCheckMetadata = "Duende.Bff.Yarp.AntiforgeryCheck";

        /// <summary>
        /// Name of optional user token metadata
        /// </summary>
        public const string OptionalUserTokenMetadata = "Duende.Bff.Yarp.OptionalUserToken";
    }

    /// <summary>
    /// Custom claim types used by Duende.BFF
    /// </summary>
    public static class ClaimTypes
    {
        /// <summary>
        /// Claim type for logout URL including session id
        /// </summary>
        public const string LogoutUrl = "bff:logout_url";

        /// <summary>
        /// Claim type for session expiration in seconds
        /// </summary>
        public const string SessionExpiresIn = "bff:session_expires_in";

        /// <summary>
        /// Claim type for authorize response session state value
        /// </summary>
        public const string SessionState = "bff:session_state";
    }

    /// <summary>
    /// Paths used for management endpoints.
    /// </summary>
    public static class ManagementEndpoints
    {
        /// <summary>
        /// Login path
        /// </summary>
        public const string Login = "/login";

        /// <summary>
        /// Silent login path
        /// </summary>
        [Obsolete("use /login?prompt=create")]
        public const string SilentLogin = "/silent-login";

        /// <summary>
        /// Silent login callback path
        /// </summary>
        public const string SilentLoginCallback = "/silent-login-callback";

        /// <summary>
        /// Logout path
        /// </summary>
        public const string Logout = "/logout";

        /// <summary>
        /// User path
        /// </summary>
        public const string User = "/user";

        /// <summary>
        /// Back channel logout path
        /// </summary>
        public const string BackChannelLogout = "/backchannel";

        /// <summary>
        /// Diagnostics path
        /// </summary>
        public const string Diagnostics = "/diagnostics";
    }

    /// <summary>
    /// Request parameter names
    /// </summary>
    public static class RequestParameters
    {
        /// <summary>
        /// Used to prevent cookie sliding on user endpoint
        /// </summary>
        public const string SlideCookie = "slide";

        /// <summary>
        /// Used to pass a return URL to login/logout
        /// </summary>
        public const string ReturnUrl = "returnUrl";

        /// <summary>
        /// Used to pass a prompt value to login
        /// </summary>
        public const string Prompt = "prompt";
    }


    /// <summary>
    /// Internal flags for library behavior
    /// </summary>
    public static class BffFlags
    {
        public const string Prompt = "bff-prompt";
    }
}
