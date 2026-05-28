// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Reads a SubjectConfirmation.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>SubjectConfirmation read</returns>
    protected SubjectConfirmation ReadSubjectConfirmation(XmlTraverser source)
    {
        var result = Create<SubjectConfirmation>();

        ReadAttributes(source, result);
        ReadElements(source.GetChildren(), result);

        return result;
    }

    /// <summary>
    /// Reads attributes of SubjectConfirmation
    /// </summary>
    /// <param name="source">Source</param>
    /// <param name="subjectConfirmation">SubjectConfirmation</param>
    protected virtual void ReadAttributes(XmlTraverser source, SubjectConfirmation subjectConfirmation) =>
        subjectConfirmation.Method = source.GetRequiredAbsoluteUriAttribute(SamlConstants.Attributes.Method);

    /// <summary>
    /// Reads elements of a SubjectConfirmation.
    /// </summary>
    /// <param name="source">Source Xml Reader</param>
    /// <param name="subjectConfirmation">Subject to populate</param>
    protected virtual void ReadElements(XmlTraverser source, SubjectConfirmation subjectConfirmation)
    {
        source.MoveNext(true);

        if (source.HasName(SamlConstants.Elements.SubjectConfirmationData, SamlConstants.Namespaces.Assertion))
        {
            subjectConfirmation.SubjectConfirmationData = ReadSubjectConfirmationData(source);
            source.MoveNext(true);
        }
    }
}
