// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    /// <summary>
    /// Metadata for an attribute consuming service.
    /// </summary>
    internal class AttributeConsumingService : IIndexedEntryWithDefault
    {
        /// <summary>
        /// Index of the endpoint
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Is this the default endpoint?
        /// </summary>
        public bool? IsDefault { get; set; }

        /// <summary>
        /// Is the service required?
        /// </summary>
        public bool? IsRequired { get; set; }

        /// <summary>
        /// The name of the attribute consuming service.
        /// </summary>
        public ICollection<LocalizedName> ServiceNames { get; private set; }
            = new Collection<LocalizedName>();

        /// <summary>
        /// Description of the attribute consuming service
        /// </summary>
        public ICollection<LocalizedName> ServiceDescriptions { get; private set; }
            = new Collection<LocalizedName>();

        /// <summary>
        /// Requested attributes.
        /// </summary>
        public ICollection<RequestedAttribute> RequestedAttributes { get; private set; }
            = new Collection<RequestedAttribute>();
    }
}
