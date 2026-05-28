// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Internal.Saml.Sp.Tokens
{
    class TokenReplayCache : ITokenReplayCache
    {
        readonly MemoryCache cache = new MemoryCache(new MemoryCacheOptions());

        // Dummy object to store in cache.
        private static readonly object cacheObject = new object();

        public bool TryAdd(string securityToken, DateTime expiresOn)
            => cache.Get(securityToken) == null &&
               cache.Set(securityToken, cacheObject, new DateTimeOffset(expiresOn)) == cacheObject;

        public bool TryFind(string securityToken)
        {
            return cache.Get(securityToken) != null;
        }
    }
}
