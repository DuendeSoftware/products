// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Reads a SubjectConfirmationData.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>SubjectConfirmationData read</returns>
    protected SubjectConfirmationData ReadSubjectConfirmationData(XmlTraverser source)
    {
        var result = Create<SubjectConfirmationData>();

        ReadAttributes(source, result);

        return result;
    }

    /// <summary>
    /// Read SubjectConfirmationData attributes.
    /// </summary>
    /// <param name="source">Source</param>
    /// <param name="subjectConfirmationData">SubjectConfirmationData</param>
    protected virtual void ReadAttributes(XmlTraverser source, SubjectConfirmationData subjectConfirmationData)
    {
        subjectConfirmationData.NotBefore = source.GetDateTimeAttribute(SamlConstants.Attributes.NotBefore);
        subjectConfirmationData.NotOnOrAfter = source.GetDateTimeAttribute(SamlConstants.Attributes.NotOnOrAfter);
        subjectConfirmationData.Recipient = source.GetAttribute(SamlConstants.Attributes.Recipient);
        subjectConfirmationData.InResponseTo = source.GetAttribute(SamlConstants.Attributes.InResponseTo);
        subjectConfirmationData.Address = source.GetAttribute(SamlConstants.Attributes.Address);
    }
}
