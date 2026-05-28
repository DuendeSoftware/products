// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal abstract class WebServiceDescriptor : RoleDescriptor
    {
        public bool? AutomaticPseudonyms { get; set; }
        public ICollection<Uri> ClaimDialectsOffered { get; private set; } =
            new Collection<Uri>();
        public ICollection<DisplayClaim> ClaimTypesOffered { get; private set; } =
            new Collection<DisplayClaim>();
        public ICollection<DisplayClaim> ClaimTypesRequested { get; private set; } =
            new Collection<DisplayClaim>();
        public ICollection<Uri> LogicalServiceNamesOffered { get; private set; } =
            new Collection<Uri>();
        public string ServiceDescription { get; set; }
        public string ServiceDisplayName { get; set; }
        public ICollection<EndpointReference> TargetScopes { get; private set; } =
            new Collection<EndpointReference>();
        public ICollection<Uri> TokenTypesOffered { get; private set; } =
            new Collection<Uri>();

        protected WebServiceDescriptor()
        {
        }
    }

}
