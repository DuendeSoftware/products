// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Controls which categories of diagnostic events are raised to the registered
/// <c>IEventSink</c>.
/// </summary>
/// <remarks>
/// All event categories default to <c>false</c>. Enable only the categories relevant to your
/// monitoring or auditing requirements to avoid unnecessary overhead.
/// </remarks>
public class EventsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether success events are enabled, which are raised when valid requests are processed without errors.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. Success events are those whose class names end with
    /// <c>SuccessEvent</c>. They are analogous to HTTP 200 responses.
    /// </remarks>
    public bool RaiseSuccessEvents { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether failure events are enabled, which are raised when a request fails due to incorrect or
    /// malformed parameters.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. Failure events are those whose class names end with
    /// <c>FailureEvent</c>. They indicate that the caller (user or client) did something wrong
    /// and are analogous to HTTP 400 responses.
    /// </remarks>
    public bool RaiseFailureEvents { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether information events are enabled, which are raised for noteworthy actions that are neither
    /// successes nor failures.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. Examples include a user granting, denying, or revoking
    /// consent — valid choices that do not represent an error condition.
    /// </remarks>
    public bool RaiseInformationEvents { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether error events are enabled, which are raised when an unexpected error occurs due to invalid
    /// configuration or an unhandled exception.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. Error events indicate a problem within IdentityServer or its
    /// configuration and are analogous to HTTP 500 responses.
    /// </remarks>
    public bool RaiseErrorEvents { get; set; }
}
