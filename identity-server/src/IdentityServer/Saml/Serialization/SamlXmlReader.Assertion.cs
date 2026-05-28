// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

public partial class SamlXmlReader
{
    /// <inheritdoc/>
    public Task<Assertion> ReadAssertionAsync(
        XmlTraverser source,
        Ct ct) =>
        ReadAssertionInternalAsync(source, errorInspector: null, ct);

    /// <inheritdoc/>
    public Task<Assertion> ReadAssertionAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<Assertion>> errorInspector,
        Ct ct) =>
        ReadAssertionInternalAsync(source, errorInspector, ct);

    private async Task<Assertion> ReadAssertionInternalAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<Assertion>>? errorInspector,
        Ct ct)
    {
        Assertion assertion = default!;

        if (source.EnsureName(SamlConstants.Elements.Assertion, SamlConstants.Namespaces.Assertion))
        {
            assertion = await ReadAssertionCoreAsync(source, ct);
            source.MoveNext(true);
        }

        CallErrorInspector(errorInspector, assertion, source);

        source.ThrowOnErrors();

        return assertion;
    }

    /// <summary>
    /// Read an <see cref="Assertion"/>
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="ct">Cancellation token</param>
    protected async Task<Assertion> ReadAssertionCoreAsync(XmlTraverser source, Ct ct)
    {
        var assertion = Create<Assertion>();

        ReadAttributes(source, assertion);
        await ReadElementsAsync(source.GetChildren(), assertion, ct);

        return assertion;
    }

    /// <summary>
    /// Read attributes of an assertion
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="assertion"></param>
    protected virtual void ReadAttributes(XmlTraverser source, Assertion assertion)
    {
        assertion.Id = source.GetRequiredAttribute(SamlConstants.Attributes.ID);
        assertion.IssueInstant = source.GetRequiredDateTimeAttribute(SamlConstants.Attributes.IssueInstant);
        assertion.Version = source.GetRequiredAttribute(SamlConstants.Attributes.Version);
    }

    /// <summary>
    /// Read elements of an assertion
    /// </summary>
    /// <param name="source">Xml traverser to read from</param>
    /// <param name="assertion">Assertion to populate</param>
    /// <param name="ct">Cancellation token</param>
    protected virtual async Task ReadElementsAsync(XmlTraverser source, Assertion assertion, Ct ct)
    {
        source.MoveNext();

        if (source.EnsureName(SamlConstants.Elements.Issuer, SamlConstants.Namespaces.Assertion))
        {
            assertion.Issuer = ReadNameId(source);
            source.MoveNext();
        }

        (var trustedSigningKeys, var allowedHashAlgorithms) =
            await GetSignatureValidationParametersFromIssuerAsync(source, assertion.Issuer, ct);

        if (source.ReadAndValidateOptionalSignature(trustedSigningKeys, allowedHashAlgorithms))
        {
            source.MoveNext();
        }
        // Set this regardles if a signature was read - the TrustLevel could be inherited
        // from a signature on the enclosing Response.
        assertion.TrustLevel = source.TrustLevel;

        if (source.EnsureName(SamlConstants.Elements.Subject, SamlConstants.Namespaces.Assertion))
        {
            assertion.Subject = ReadSubject(source);
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.Conditions, SamlConstants.Namespaces.Assertion))
        {
            assertion.Conditions = ReadConditions(source);
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.Advice, SamlConstants.Namespaces.Assertion))
        {
            // We're not supporting Advice
            source.IgnoreChildren();
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.AuthnStatement, SamlConstants.Namespaces.Assertion))
        {
            assertion.AuthnStatement = ReadAuthnStatement(source);
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.AuthzDecisionStatement, SamlConstants.Namespaces.Assertion))
        {
            // Not supporting AuthzDecisionStatement, skip it
            source.IgnoreChildren();
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.AttributeStatement, SamlConstants.Namespaces.Assertion))
        {
            var attributes = source.GetChildren();

            if (attributes.MoveNext(false))
            {
                do
                {
                    if (attributes.EnsureName(SamlConstants.Elements.Attribute, SamlConstants.Namespaces.Assertion))
                    {
                        assertion.Attributes.Add(ReadAttribute(attributes));
                    }
                } while (attributes.MoveNext(true));
            }
            source.MoveNext(true);
        }
    }
}
