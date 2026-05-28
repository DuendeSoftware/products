// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;
using System.Xml;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class ContactPerson
    {
        public string Company { get; set; }
        public ICollection<string> EmailAddresses { get; private set; } =
            new Collection<string>();
        public string GivenName { get; set; }
        public string Surname { get; set; }
        public ICollection<string> TelephoneNumbers { get; private set; } =
            new Collection<string>();
        public ContactType Type { get; set; }
        public ICollection<XmlElement> Extensions { get; private set; } =
            new Collection<XmlElement>();

        public ContactPerson()
        {
        }

        public ContactPerson(ContactType type)
        {
            Type = type;
        }
    }
}
