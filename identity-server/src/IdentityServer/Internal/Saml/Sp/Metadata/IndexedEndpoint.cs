// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class IndexedEndpoint : Endpoint, IIndexedEntryWithDefault
    {
        /// <summary>
        /// Index of the endpoint
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Is this the default endpoint?
        /// </summary>
        public bool? IsDefault { get; set; }
    }
}
