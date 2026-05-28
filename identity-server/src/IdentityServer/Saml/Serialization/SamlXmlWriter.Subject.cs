// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append a Subject element
    /// </summary>
    /// <param name="parent">Parent node</param>
    /// <param name="subject">Subject</param>
    protected virtual void Append(XmlNode parent, Subject subject)
    {
        var subjectElement = AppendElement(parent, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.Subject);
        if (subject.NameId != null)
        {
            Append(subjectElement, subject.NameId, SamlConstants.Elements.NameID);
        }

        if (subject.SubjectConfirmation != null)
        {
            Append(subjectElement, subject.SubjectConfirmation);
        }
    }

    /// <summary>
    /// Append a SubjectConfirmation element
    /// </summary>
    /// <param name="parent">Parent node</param>
    /// <param name="subjectConfirmation">Write subjectConfirmation</param>
    protected virtual void Append(XmlNode parent, SubjectConfirmation subjectConfirmation)
    {
        var subjectConfirmationElement = AppendElement(parent, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.SubjectConfirmation);
        subjectConfirmationElement.SetAttribute(SamlConstants.Attributes.Method, subjectConfirmation.Method);

        if (subjectConfirmation.SubjectConfirmationData != null)
        {
            Append(subjectConfirmationElement, subjectConfirmation.SubjectConfirmationData);
        }
    }

    /// <summary>
    /// Append a SubjectConfirmationData element
    /// </summary>
    /// <param name="parent">Parent node</param>
    /// <param name="subjectConfirmationData">Write subjectConfirmationData</param>
    protected virtual void Append(XmlNode parent, SubjectConfirmationData subjectConfirmationData)
    {
        var subjectConfirmationDataElement = AppendElement(parent, SamlConstants.Namespaces.SamlPrefix, SamlConstants.Elements.SubjectConfirmationData);
        subjectConfirmationDataElement.SetAttributeIfValue(SamlConstants.Attributes.NotOnOrAfter, subjectConfirmationData.NotOnOrAfter);
        subjectConfirmationDataElement.SetAttributeIfValue(SamlConstants.Attributes.Recipient, subjectConfirmationData.Recipient);
        subjectConfirmationDataElement.SetAttributeIfValue(SamlConstants.Attributes.InResponseTo, subjectConfirmationData.InResponseTo);
    }
}
