// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Commands
{
    /// <summary>
    /// Reimplementation of System.Web.HttpCacheability.
    /// </summary>
    internal enum Cacheability
    {
        /// <summary>
        /// Value is not initialized and probably a mistake. 
        /// </summary>
        NotSpecified = 0,
        /// <summary>
        /// Sets the Cache-Control: no-cache header.
        /// </summary>
        NoCache = 1,
        /// <summary>
        /// The default value. Sets the cache control to "private".
        /// </summary>
        Private = 2,
        /// <summary>
        /// Specifies that the response is cached only at the origin server.
        /// </summary>
        Server = 3,
        /// <summary>
        /// Will disallow anyone but the server to cache the result.
        /// </summary>
        ServerAndNoCache = 3,
        /// <summary>
        /// Sets the Cache-Control to public.
        /// </summary>
        Public = 4,
        /// <summary>
        /// The response is cached in the client and the server but nowhere else.
        /// </summary>
        ServerAndPrivate = 5
    }
}
