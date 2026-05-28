// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class DiscoveryResponse : IndexedEndpoint
    {
    }

    internal class SpSsoDescriptor : SsoDescriptor
    {
        public IndexedCollectionWithDefault<AssertionConsumerService> AssertionConsumerServices { get; private set; } =
            new IndexedCollectionWithDefault<AssertionConsumerService>();
        public IndexedCollectionWithDefault<AttributeConsumingService> AttributeConsumingServices { get; private set; } =
            new IndexedCollectionWithDefault<AttributeConsumingService>();
        public bool? AuthnRequestsSigned { get; set; }
        public bool? WantAssertionsSigned { get; set; }
        public IndexedCollectionWithDefault<DiscoveryResponse> DiscoveryResponses { get; private set; } =
            new IndexedCollectionWithDefault<DiscoveryResponse>();
    }
}
