// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Controls how a coordinated background job's lease is sized and renewed. These options only have
/// an effect when IdentityServer coordinates the job across nodes, which requires Duende Storage
/// (the default storage instance) or Spaces to be registered. Without either, the job runs on every
/// node and these options are ignored.
/// </summary>
public class BackgroundJobLeaseOptions
{
    /// <summary>
    /// Gets or sets how long the job's lease is valid for before it expires, absent renewal.
    /// </summary>
    /// <remarks>
    /// Defaults to 3 times the job's own run interval, clamped to between 30 seconds and 5 minutes.
    /// A node that stops without releasing its lease (for example, a crash) blocks the job on other
    /// nodes for at most this long.
    /// </remarks>
    public TimeSpan? LeaseDuration { get; set; }

    /// <summary>
    /// Gets or sets how often the holding node renews its lease while it runs.
    /// </summary>
    /// <remarks>
    /// Defaults to one third of <see cref="LeaseDuration"/>.
    /// </remarks>
    public TimeSpan? RenewalInterval { get; set; }

    /// <summary>
    /// Gets or sets how long before expiry the holding node gives up on renewal and treats the lease
    /// as lost, stopping any in-progress run.
    /// </summary>
    /// <remarks>
    /// Defaults to the greater of one tenth of <see cref="LeaseDuration"/> and 5 seconds. This value
    /// must exceed the clock skew between nodes in the cluster: IdentityServer does not enforce this,
    /// because it does not know the skew.
    /// </remarks>
    public TimeSpan? CancelBeforeExpiry { get; set; }
}
