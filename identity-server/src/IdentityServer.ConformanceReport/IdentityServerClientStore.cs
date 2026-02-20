// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.ConformanceReport;

/// <summary>
/// Adapts IdentityServer's client store to the conformance client store interface.
/// </summary>
#pragma warning disable CA1812 // IdentityServerClientStore is instantiated via DI
internal sealed class IdentityServerClientStore(IClientStore clientStore) : IConformanceReportClientStore
#pragma warning restore CA1812
{
    public async Task<IEnumerable<ConformanceReportClient>> GetAllClientsAsync(
        CancellationToken ct = default)
    {
        var clients = new List<ConformanceReportClient>();
        await foreach (var client in clientStore.GetAllClientsAsync(ct))
        {
            clients.Add(client.ToConformanceReportClient());
        }
        return clients;
    }
}
