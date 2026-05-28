// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Runtime.CompilerServices;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Default implementation of <see cref="IConnectedApplicationStore"/> that composes
/// <see cref="IClientStore"/> and <see cref="ISamlServiceProviderStore"/> to provide
/// a unified read-only view over all registered applications.
/// </summary>
public sealed class ConnectedApplicationStore : IConnectedApplicationStore
{
    private readonly IClientStore _clientStore;
    private readonly ISamlServiceProviderStore _samlStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectedApplicationStore"/> class.
    /// </summary>
    /// <param name="clientStore">The OIDC client store.</param>
    /// <param name="samlStore">The SAML Service Provider store.</param>
    public ConnectedApplicationStore(IClientStore clientStore, ISamlServiceProviderStore samlStore)
    {
        _clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));
        _samlStore = samlStore ?? throw new ArgumentNullException(nameof(samlStore));
    }

    /// <inheritdoc/>
    public async Task<IConnectedApplication?> FindByIdentifierAsync(string identifier, Ct ct)
    {
        var client = await _clientStore.FindClientByIdAsync(identifier, ct);
        if (client is not null)
        {
            return client;
        }

        return await _samlStore.FindByEntityIdAsync(identifier, ct);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IConnectedApplication> GetAllAsync([EnumeratorCancellation] Ct ct)
    {
        await foreach (var client in _clientStore.GetAllClientsAsync(ct))
        {
            yield return client;
        }

        await foreach (var sp in _samlStore.GetAllSamlServiceProvidersAsync(ct))
        {
            yield return sp;
        }
    }
}
