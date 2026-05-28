// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <summary>
    /// Reads a Subject.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>Subject read</returns>
    protected Subject ReadSubject(XmlTraverser source)
    {
        var result = Create<Subject>();

        ReadElements(source.GetChildren(), result);

        return result;
    }

    /// <summary>
    /// Reads elements of a subject.
    /// </summary>
    /// <param name="source">Source Xml Reader</param>
    /// <param name="subject">Subject to populate</param>
    protected virtual void ReadElements(XmlTraverser source, Subject subject)
    {
        source.MoveNext(true);

        if (source.HasName(SamlConstants.Elements.NameID, SamlConstants.Namespaces.Assertion))
        {
            subject.NameId = ReadNameId(source);
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.SubjectConfirmation, SamlConstants.Namespaces.Assertion))
        {
            subject.SubjectConfirmation = ReadSubjectConfirmation(source);
            source.MoveNext(true);
        }
    }
}
