// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.AspNetCore.Authentication.JwtBearer;

public class TestDPoPNonceStore : IDPoPNonceStore
{
    private string _nonce = string.Empty;
    public Task<string?> GetNonceAsync(DPoPNonceContext context, CancellationToken cancellationToken = new()) => Task.FromResult<string?>(_nonce);

    public Task StoreNonceAsync(DPoPNonceContext context, string nonce, CancellationToken cancellationToken = new())
    {
        _nonce = nonce;
        return Task.CompletedTask;
    }
}
