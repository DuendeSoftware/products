// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class ApplicationServiceDescriptor : WebServiceDescriptor
    {
        public ICollection<EndpointReference> Endpoints { get; private set; } =
            new List<EndpointReference>();
        public ICollection<EndpointReference> PassiveRequestorEndpoints { get; private set; } =
            new List<EndpointReference>();
        public ICollection<EndpointReference> SingleSignOutEndpoints { get; private set; } =
            new List<EndpointReference>();

        public ApplicationServiceDescriptor()
        {
        }

        public ApplicationServiceDescriptor(
            IEnumerable<EndpointReference> endpoints,
            IEnumerable<EndpointReference> passiveRequestorEndpoints,
            IEnumerable<EndpointReference> singleSignOutEndpoints
        )
        {
            ((List<EndpointReference>)Endpoints).AddRange(endpoints);
        }
    }
}
