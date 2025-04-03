// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Blazor.Client;

/// <summary>
/// Constants for Duende.BFF
/// </summary>
public static class Constants
{
    /// <summary>
    /// Custom claim types used by Duende.BFF
    /// </summary>
    public static class ClaimTypes
    {
        /// <summary>
        /// Claim type for logout URL including session id
        /// </summary>
        public const string LogoutUrl = "bff:logout_url";
    }
}
