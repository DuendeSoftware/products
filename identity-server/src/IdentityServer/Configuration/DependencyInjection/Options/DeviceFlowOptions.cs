// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for the OAuth 2.0 Device Authorization Grant (device flow), which allows
/// input-constrained devices to obtain tokens via a secondary device.
/// </summary>
public class DeviceFlowOptions
{
    /// <summary>
    /// Gets or sets the default user code type used when generating device flow user codes, unless overridden
    /// at the client level.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="IdentityServerConstants.UserCodeTypes.Numeric"/>, which produces
    /// a 9-digit numeric code.
    /// </remarks>
    public string DefaultUserCodeType { get; set; } = IdentityServerConstants.UserCodeTypes.Numeric;

    /// <summary>
    /// Gets or sets the minimum polling interval, in seconds, that clients must respect when polling the
    /// token endpoint during a device flow.
    /// </summary>
    /// <remarks>
    /// Defaults to 5 seconds. Clients that poll more frequently will receive a
    /// <c>slow_down</c> error response.
    /// </remarks>
    public int Interval { get; set; } = 5;
}
