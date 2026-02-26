// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Services.Default.KeyManagement;

internal class MockSigningKeyStore : ISigningKeyStore
{
    public List<SerializedKey> Keys { get; set; } = new List<SerializedKey>();
    public bool LoadKeysAsyncWasCalled { get; set; }
    public bool DeleteWasCalled { get; set; }

    public Task DeleteKeyAsync(string id, Ct _)
    {
        DeleteWasCalled = true;
        if (Keys != null)
        {
            Keys.Remove(Keys.FirstOrDefault(x => x.Id == id));
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<SerializedKey>> LoadKeysAsync(Ct _)
    {
        LoadKeysAsyncWasCalled = true;
        return Task.FromResult<IEnumerable<SerializedKey>>(Keys);
    }

    public Task StoreKeyAsync(SerializedKey key, Ct _)
    {
        if (Keys == null)
        {
            Keys = new List<SerializedKey>();
        }

        Keys.Add(key);
        return Task.CompletedTask;
    }
}
