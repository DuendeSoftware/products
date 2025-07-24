// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Configures events
/// </summary>
public class EventsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to raise success events.
    /// </summary>
    /// <value>
    ///   <c>true</c> if success event should be raised; otherwise, <c>false</c>.
    /// </value>
    public bool RaiseSuccessEvents { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to raise failure events.
    /// </summary>
    /// <value>
    ///   <c>true</c> if failure events should be raised; otherwise, <c>false</c>.
    /// </value>
    public bool RaiseFailureEvents { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to raise information events.
    /// </summary>
    /// <value>
    /// <c>true</c> if information events should be raised; otherwise, <c>false</c>.
    /// </value>
    public bool RaiseInformationEvents { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to raise error events.
    /// </summary>
    /// <value>
    ///   <c>true</c> if error events should be raised; otherwise, <c>false</c>.
    /// </value>
    public bool RaiseErrorEvents { get; set; }
}
