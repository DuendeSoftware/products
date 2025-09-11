// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;

namespace Duende.AspNetCore.Authentication.JwtBearer;

public class TestDPoPNonceStore : IDPoPNonceStore
{
    private DPoPNonce _nonce = DPoPNonce.Parse(string.Empty);
    public Task<DPoPNonce?> GetNonceAsync(DPoPNonceContext context, CancellationToken cancellationToken = new()) => Task.FromResult<DPoPNonce?>(_nonce);

    public Task StoreNonceAsync(DPoPNonceContext context, DPoPNonce nonce, CancellationToken cancellationToken = new())
    {
        _nonce = nonce;
        return Task.CompletedTask;
    }
}
