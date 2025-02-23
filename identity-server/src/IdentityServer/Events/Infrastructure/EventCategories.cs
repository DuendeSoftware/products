// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Events;

/// <summary>
/// Categories for events
/// </summary>
public static class EventCategories
{
    /// <summary>
    /// Authentication related events
    /// </summary>
    public const string Authentication = "Authentication";

    /// <summary>
    /// Backchannel authentication related events
    /// </summary>
    public const string BackchannelAuthentication = "BackchannelAuthentication";

    /// <summary>
    /// Token related events
    /// </summary>
    public const string Token = "Token";

    /// <summary>
    /// Grants related events
    /// </summary>
    public const string Grants = "Grants";

    /// <summary>
    /// Error related events
    /// </summary>
    public const string Error = "Error";

    /// <summary>
    /// Device flow related events
    /// </summary>
    public const string DeviceFlow = "Device";
}