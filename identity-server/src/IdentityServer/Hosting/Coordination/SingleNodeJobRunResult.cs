// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Hosting.Coordination;

/// <summary>
/// The outcome of one <see cref="SingleNodeJob.RunAsync"/> call.
/// </summary>
internal enum SingleNodeJobRunResult
{
    /// <summary>The work delegate ran.</summary>
    Ran,

    /// <summary>Another node holds the lease; the work delegate was not invoked.</summary>
    SkippedNotLeaseHolder,

    /// <summary>The lease was lost while the work delegate was running.</summary>
    LeaseLost,

    /// <summary>
    /// The lock could not be reached (it threw while trying to acquire); the work delegate was not
    /// invoked. Fails closed: a job that can't prove it is the only runner doesn't run.
    /// </summary>
    LockUnavailable,
}
