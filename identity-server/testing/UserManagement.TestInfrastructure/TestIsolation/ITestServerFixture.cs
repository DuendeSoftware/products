// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.TestIsolation;

/// <summary>
/// Shared contract for assembly-level test server fixtures (e.g. <c>WebServerFixture</c>)
/// so that <see cref="KestrelBasedTestServer"/> can accept them without coupling to a
/// concrete type.
/// </summary>
public interface ITestServerFixture
{
    /// <summary>
    /// The port the shared Kestrel host is listening on.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// The <see cref="TestIsolationService"/> that manages per-test DI containers.
    /// </summary>
    TestIsolationService IsolationService { get; }
}
