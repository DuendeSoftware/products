// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

/// <summary>
/// Controls how long a lease is held and when it is renewed. Ignored by <see cref="InProcessDistributedLock"/>,
/// whose leases don't outlive the process.
/// </summary>
/// <param name="Duration">How long the lease is valid for before it expires, absent renewal.</param>
/// <param name="RenewalInterval">How often the lease is renewed while it is held.</param>
/// <param name="CancelBeforeExpiry">
/// How long before expiry to cancel <see cref="ILockLease.Lost"/> if renewal hasn't succeeded. Must exceed
/// clock skew between nodes (not enforced).
/// </param>
internal sealed record LeaseTiming(TimeSpan Duration, TimeSpan RenewalInterval, TimeSpan CancelBeforeExpiry)
{
    /// <summary>Gets the default timing: 30 second lease, renewed every 10 seconds, lost 5 seconds before expiry.</summary>
    public static LeaseTiming Default { get; } = new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
}
