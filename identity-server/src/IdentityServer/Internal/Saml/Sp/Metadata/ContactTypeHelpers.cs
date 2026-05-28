// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal static class ContactTypeHelpers
    {
        public static ContactType Parse(string contactType)
        {
            if (contactType == null)
            {
                throw new ArgumentNullException(nameof(contactType));
            }

            switch (contactType)
            {
                case "technical":
                    return ContactType.Technical;
                case "support":
                    return ContactType.Support;
                case "administrative":
                    return ContactType.Administrative;
                case "billing":
                    return ContactType.Billing;
                case "other":
                    return ContactType.Other;
                default:
                    throw new FormatException($"Unknown contactType value '{contactType}'");
            }
        }

        public static string ToString(ContactType contactType)
        {
            switch (contactType)
            {
                case ContactType.Technical:
                    return "technical";
                case ContactType.Support:
                    return "support";
                case ContactType.Administrative:
                    return "administrative";
                case ContactType.Billing:
                    return "billing";
                case ContactType.Other:
                    return "other";
                default:
                    throw new InvalidOperationException(
                        $"Unknown ContactType enumeration value {contactType}");
            }
        }
    }
}
