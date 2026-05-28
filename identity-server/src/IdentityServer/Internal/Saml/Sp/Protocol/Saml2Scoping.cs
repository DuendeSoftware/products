// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// Saml2Scoping specifies a set of identity providers trusted by the
    /// requester to authenticate the presenter, as well as limitations and
    /// context related to proxying of the authentication request message to
    /// subsequent identity providers by the responder.
    /// </summary>
    internal class Saml2Scoping
    {
        /// <summary>
        /// Gets advisory list of identity providers and associated information 
        /// that the requester deems acceptable to respond to the request.
        /// </summary>
        public IList<Saml2IdpEntry> IdPEntries { get; } = new List<Saml2IdpEntry>();

        /// <summary>
        /// Fluent config helper that adds a <see cref="Saml2IdpEntry"/> to the 
        /// <see cref="Saml2Scoping"/>
        /// </summary>
        /// <param name="idpEntry">Idp entry to add</param>
        /// <returns>this</returns>
        public Saml2Scoping With(Saml2IdpEntry idpEntry)
        {
            IdPEntries.Add(idpEntry);
            return this;
        }

        private int? proxyCount;

        /// <summary>
        /// Specifies the number of proxying indirections permissible between
        /// the identity provider that receives the authentication request and
        /// the identity provider who ultimately authenticates the principal.
        /// A count of zero permits no proxying, while omitting (null) this
        /// attribute expresses no such restriction.
        /// </summary>
        public int? ProxyCount
        {
            get
            {
                return proxyCount;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("ProxyCount cannot be negative.");
                }
                proxyCount = value;
            }
        }

        /// <summary>
        /// Gets or sets the set of requesting entities on whose behalf the
        /// requester is acting. Used to communicate the chain of requesters
        /// when proxying occurs.
        /// </summary>
        public IList<EntityId> RequesterIds { get; } = new List<EntityId>();

        /// <summary>
        /// Fluent config helper that adds a requester id to the
        /// <see cref="Saml2Scoping"/>
        /// </summary>
        /// <param name="requesterId">Requester Id to add</param>
        /// <returns>this</returns>
        public Saml2Scoping WithRequesterId(EntityId requesterId)
        {
            RequesterIds.Add(requesterId);
            return this;
        }

        /// <summary>
        /// Create XElement for the Saml2Scoping.
        /// </summary>
        public XElement ToXElement()
        {
            var scopingElement = new XElement(Saml2Namespaces.Saml2P + "Scoping");

            if (ProxyCount.HasValue && ProxyCount.Value >= 0)
            {
                scopingElement.AddAttributeIfNotNullOrEmpty("ProxyCount", ProxyCount);
            }

            if (IdPEntries.Count > 0)
            {
                scopingElement.Add(new XElement(
                    Saml2Namespaces.Saml2P + "IDPList",
                    IdPEntries.Select(x => x.ToXElement())));
            }

            scopingElement.Add(RequesterIds.Select(x =>
            new XElement(Saml2Namespaces.Saml2P + "RequesterID", x.Id)));

            return scopingElement;
        }
    }
}

