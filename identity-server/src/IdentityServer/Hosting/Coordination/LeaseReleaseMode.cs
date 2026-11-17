// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Hosting.Coordination;

/// <summary>
/// Controls whether a <see cref="SingleNodeJob"/> keeps its lease between runs or releases it after
/// every run. This is a correctness property of each job, fixed in code, not an option.
/// </summary>
internal enum LeaseReleaseMode
{
    /// <summary>Release the lease as soon as the current run completes, so another node can run next.</summary>
    AfterEachRun,

    /// <summary>Keep the lease across runs while it is still held, so the job keeps any in-memory state valid.</summary>
    KeepAcrossRuns,
}
