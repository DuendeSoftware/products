// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// One load-balanced IdentityServer (design doc section 05): 2 nodes (D36) behind a <see cref="TestLoadBalancer" />.
/// The system under test is one, and isn't exposed as a handle (D37); every upstream added with
/// <see cref="TestEnvironmentBuilder.AddRemoteIdentityServer" /> is another.
/// </summary>
public interface IIdentityServer
{
    /// <summary>The name the server was added with. Its URL appears as <c>{name}</c> in snapshots.</summary>
    string Name { get; }

    /// <summary>The public URL: the load balancer's virtual address, for example <c>http://upstream</c>.</summary>
    Uri BaseUrl { get; }

    /// <summary>
    /// Seeds and changes this server's own data, for example the users of a remote IdP (D40). The system under test's
    /// data is written through <see cref="IIssuer.Storage" /> instead.
    /// </summary>
    ITestStorage Storage { get; }

    /// <summary>The nodes behind the load balancer. Only for routing in regression tests (D12). Available after start.</summary>
    IReadOnlyList<IdentityServerNode> Nodes { get; }

    /// <summary>The reverse proxy in front of <see cref="Nodes" />. Only for routing in regression tests. Available after start.</summary>
    TestLoadBalancer LoadBalancer { get; }
}
