// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml.Linq;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Extension methods for Saml2Subject
    /// </summary>
    internal static class Saml2SubjectExtensions
    {
        extension(Saml2Subject subject)
        {
            /// <summary>
            /// Writes out the subject as an XElement.
            /// </summary>
            /// <returns>XElement</returns>
            public XElement ToXElement()
            {
                if (subject == null)
                {
                    throw new ArgumentNullException(nameof(subject));
                }

                var element = new XElement(Saml2Namespaces.Saml2 + "Subject",
                    subject.NameId.ToXElement());

                foreach (var subjectConfirmation in subject.SubjectConfirmations)
                {
                    element.Add(subjectConfirmation.ToXElement());
                }

                if (subject.SubjectConfirmations.Count == 0)
                {
                    // Although SubjectConfirmation is optional in the SAML core spec, it is
                    // mandatory in the Web Browser SSO Profile and must have a value of bearer.
                    element.Add(new Saml2SubjectConfirmation(
                        new Uri("urn:oasis:names:tc:SAML:2.0:cm:bearer")).ToXElement());
                }

                return element;
            }
        }

        extension(Saml2SubjectConfirmation subjectConfirmation)
        {
            /// <summary>
            /// Writes out the subject confirmation as an XElement.
            /// </summary>
            /// <returns></returns>
            /// <exception cref="ArgumentNullException"></exception>
            public XElement ToXElement()
            {
                if (subjectConfirmation == null)
                {
                    throw new ArgumentNullException(nameof(subjectConfirmation));
                }

                var element = new XElement(Saml2Namespaces.Saml2 + "SubjectConfirmation",
                    new XAttribute("Method", subjectConfirmation.Method.OriginalString));

                if (subjectConfirmation.SubjectConfirmationData != null)
                {
                    element.Add(subjectConfirmation.SubjectConfirmationData.ToXElement());
                }

                return element;
            }
        }

        extension(Saml2SubjectConfirmationData subjectConfirmationData)
        {
            /// <summary>
            /// Writes out the subject confirmation data as an XElement.
            /// </summary>
            /// <returns></returns>
            /// <exception cref="ArgumentNullException"></exception>
            public XElement ToXElement()
            {
                if (subjectConfirmationData == null)
                {
                    throw new ArgumentNullException(nameof(subjectConfirmationData));
                }

                var element = new XElement(Saml2Namespaces.Saml2 + "SubjectConfirmationData");

                if (subjectConfirmationData.NotOnOrAfter.HasValue)
                {
                    element.SetAttributeValue("NotOnOrAfter",
                        subjectConfirmationData.NotOnOrAfter.Value.ToSaml2DateTimeString());
                }

                if (subjectConfirmationData.InResponseTo != null)
                {
                    element.SetAttributeValue("InResponseTo", subjectConfirmationData.InResponseTo.Value);
                }

                if (subjectConfirmationData.Recipient != null)
                {
                    element.SetAttributeValue("Recipient", subjectConfirmationData.Recipient.OriginalString);
                }

                if (subjectConfirmationData.NotBefore.HasValue)
                {
                    element.SetAttributeValue("NotBefore",
                        subjectConfirmationData.NotBefore.Value.ToSaml2DateTimeString());
                }

                return element;
            }
        }
    }
}
