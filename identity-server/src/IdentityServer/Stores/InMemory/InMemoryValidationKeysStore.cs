// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// The default validation key store
/// </summary>
/// <seealso cref="IValidationKeysStore" />
public class InMemoryValidationKeysStore : IValidationKeysStore
{
    private readonly IReadOnlyCollection<SecurityKeyInfo> _keys;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryValidationKeysStore"/> class.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <exception cref="System.ArgumentNullException">keys</exception>
    public InMemoryValidationKeysStore(IEnumerable<SecurityKeyInfo> keys) => _keys = (keys ?? throw new ArgumentNullException(nameof(keys))).ToArray();

    /// <summary>
    /// Gets all validation keys.
    /// </summary>
    /// <returns></returns>
    public Task<IReadOnlyCollection<SecurityKeyInfo>> GetValidationKeysAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryValidationKeysStore.GetValidationKeys");

        return Task.FromResult(_keys);
    }
}
