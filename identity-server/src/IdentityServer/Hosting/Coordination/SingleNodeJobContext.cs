// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Hosting.Coordination;

/// <summary>
/// Passed to the work delegate of a <see cref="SingleNodeJob"/> run.
/// </summary>
/// <param name="CancellationToken">
/// Cancelled when the host's own stopping token is cancelled, or when the lease is lost while the
/// run is in progress (distributed jobs only).
/// </param>
/// <param name="LeaseNewlyAcquired">
/// <see langword="true"/> when this run is the first with the current lease: either the very first
/// run (not distributed), or the run right after a lease was freshly acquired (distributed,
/// including after a reacquire following a lost lease). <see langword="false"/> on every later run
/// that keeps the same held lease.
/// </param>
#pragma warning disable CA1068 // The order follows the spec: (CancellationToken, LeaseNewlyAcquired).
internal readonly record struct SingleNodeJobContext(Ct CancellationToken, bool LeaseNewlyAcquired);
#pragma warning restore CA1068
