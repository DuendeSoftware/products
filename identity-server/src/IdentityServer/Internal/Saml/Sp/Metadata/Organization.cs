// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;
using System.Xml;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class Organization
    {
        public ICollection<XmlElement> Extensions { get; private set; } =
            new Collection<XmlElement>();
        public LocalizedEntryCollection<LocalizedName> DisplayNames { get; private set; } =
            new LocalizedEntryCollection<LocalizedName>();
        public LocalizedEntryCollection<LocalizedName> Names { get; private set; } =
            new LocalizedEntryCollection<LocalizedName>();
        public LocalizedEntryCollection<LocalizedUri> Urls { get; private set; } =
            new LocalizedEntryCollection<LocalizedUri>();
    }
}
