// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class SecurityTokenServiceDescriptor : WebServiceDescriptor
    {
        public Collection<EndpointReference> SecurityTokenServiceEndpoints { get; private set; } =
            new Collection<EndpointReference>();
        public Collection<EndpointReference> SingleSignOutSubscriptionEndpoints { get; private set; } =
            new Collection<EndpointReference>();
        public Collection<EndpointReference> SingleSignOutNotificationEndpoints { get; private set; } =
            new Collection<EndpointReference>();
        public Collection<EndpointReference> PassiveRequestorEndpoints { get; private set; } =
            new Collection<EndpointReference>();
    }
}
