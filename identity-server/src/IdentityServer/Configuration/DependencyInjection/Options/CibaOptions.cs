// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for Client-Initiated Backchannel Authentication (CIBA), which allows clients to
/// initiate authentication out-of-band without a browser redirect.
/// </summary>
public class CibaOptions
{
    /// <summary>
    /// Gets or sets the default lifetime of a pending CIBA authentication request, in seconds.
    /// </summary>
    /// <remarks>
    /// Defaults to 300 seconds (5 minutes). Individual clients may override this value.
    /// </remarks>
    public int DefaultLifetime { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum frequency, in seconds, at which a client may poll the token endpoint during
    /// a CIBA flow.
    /// </summary>
    /// <remarks>
    /// Defaults to 5 seconds. Clients that poll more frequently than this interval will receive
    /// a <c>slow_down</c> error response.
    /// </remarks>
    public int DefaultPollingInterval { get; set; } = 5;
}
