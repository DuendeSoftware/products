// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class Endpoint
    {
        public Uri Binding { get; set; }
        public Uri Location { get; set; }
        public Uri ResponseLocation { get; set; }

        public Endpoint()
        {
        }

        public Endpoint(Uri binding, Uri location)
        {
            Binding = binding;
            Location = location;
        }
    }
}
