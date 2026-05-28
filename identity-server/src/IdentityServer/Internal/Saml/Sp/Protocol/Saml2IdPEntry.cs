// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// The Saml2IdPEntry specifies a single identity provider trusted by the
    /// requester to authenticate the presenter    
    /// </summary>
    internal class Saml2IdpEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Saml2IdpEntry"/> class.
        /// </summary>
        /// <param name="providerId">The provider identifier.</param>
        public Saml2IdpEntry(EntityId providerId)
        {
            ProviderId = providerId;
        }

        /// <summary>
        /// A URI reference representing the location of a profile-specific
        /// endpoint supporting the authentication request protocol. The
        /// binding to be used must be understood from the profile of use.
        /// </summary>
        public Uri Location { get; set; }

        /// <summary>
        /// A human-readable name for the identity provider.
        /// </summary>
        public string Name { get; set; }

        EntityId providerId;
        /// <summary>
        /// The Entity Id of the Identity Provider. Cannot be null.
        /// </summary>
        public EntityId ProviderId
        {
            get
            {
                return providerId;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                providerId = value;
            }
        }

        /// <summary>
        /// Create XElement for the Saml2IdPEntry.
        /// </summary>
        public XElement ToXElement()
        {
            var idpEntryElement = new XElement(Saml2Namespaces.Saml2P + "IDPEntry");

            idpEntryElement.AddAttributeIfNotNullOrEmpty("ProviderID", ProviderId.Id);
            idpEntryElement.AddAttributeIfNotNullOrEmpty("Name", Name);
            idpEntryElement.AddAttributeIfNotNullOrEmpty("Loc", Location?.OriginalString);

            return idpEntryElement;
        }
    }
}
