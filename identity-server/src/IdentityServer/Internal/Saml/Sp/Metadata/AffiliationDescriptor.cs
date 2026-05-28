// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;
using System.Xml;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class AffiliationDescriptor : ICachedMetadata
    {
        public ICollection<EntityId> AffiliateMembers { get; private set; } =
            new Collection<EntityId>();
        public ICollection<XmlElement> Extensions { get; private set; } =
            new Collection<XmlElement>();
        public ICollection<KeyDescriptor> KeyDescriptors { get; private set; } =
            new Collection<KeyDescriptor>();
        public EntityId AffiliationOwnerId { get; set; }
        public DateTime? ValidUntil { get; set; }
        public XsdDuration? CacheDuration { get; set; }
        public string Id { get; set; }
    }
}
