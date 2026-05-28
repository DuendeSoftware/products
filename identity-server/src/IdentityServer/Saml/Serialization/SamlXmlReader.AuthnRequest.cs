// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

public partial class SamlXmlReader
{
    /// <inheritdoc/>
    public async Task<AuthnRequest> ReadAuthnRequestAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<AuthnRequest>>? errorInspector,
        Ct ct)
    {
        AuthnRequest authnRequest = default!;

        if (source.EnsureName(SamlConstants.Elements.AuthnRequest, SamlConstants.Namespaces.Protocol))
        {
            authnRequest = await ReadAuthnRequestCoreAsync(source, ct);
            source.MoveNext(true);
        }

        CallErrorInspector(errorInspector, authnRequest, source);

        source.ThrowOnErrors();

        return authnRequest;
    }

    /// <summary>
    /// Read an <see cref="AuthnRequest"/>
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns><see cref="AuthnRequest"/>The AuthnRequest read</returns>
    protected async Task<AuthnRequest> ReadAuthnRequestCoreAsync(XmlTraverser source, Ct ct)
    {
        var authnRequest = Create<AuthnRequest>();

        ReadAttributes(source, authnRequest);
        await ReadElementsAsync(source.GetChildren(), authnRequest, ct);

        source.MoveNext(true);

        return authnRequest;
    }

    /// <summary>
    /// Reads the child elements of an AuthnRequest.
    /// </summary>
    /// <param name="source">Xml traverser to read from</param>
    /// <param name="authnRequest">AuthnRequest to populate</param>
    /// <param name="ct">Cancellation token</param>
    protected virtual async Task ReadElementsAsync(XmlTraverser source, AuthnRequest authnRequest, Ct ct)
    {
        await ReadElementsAsync(source, (RequestAbstractType)authnRequest, ct);

        if (source.HasName(SamlConstants.Elements.Subject, SamlConstants.Namespaces.Assertion))
        {
            authnRequest.Subject = ReadSubject(source);
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.NameIDPolicy, SamlConstants.Namespaces.Protocol))
        {
            authnRequest.NameIdPolicy = ReadNameIdPolicy(source);
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.Conditions, SamlConstants.Namespaces.Assertion))
        {
            authnRequest.Conditions = ReadConditions(source);
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.RequestedAuthnContext, SamlConstants.Namespaces.Protocol))
        {
            authnRequest.RequestedAuthnContext = ReadRequestedAuthnContext(source);
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.Scoping, SamlConstants.Namespaces.Protocol))
        {
            authnRequest.Scoping = ReadScoping(source);
            source.MoveNext(true);
        }
    }

    /// <summary>
    /// Reads attributes of an AuthnRequest
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="authnRequest">The AuthnRequest to populate</param>
    protected virtual void ReadAttributes(XmlTraverser source, AuthnRequest authnRequest)
    {
        ReadAttributes(source, (RequestAbstractType)authnRequest);

        authnRequest.ForceAuthn = source.GetBoolAttribute(SamlConstants.Attributes.ForceAuthn) ?? authnRequest.ForceAuthn;
        authnRequest.IsPassive = source.GetBoolAttribute(SamlConstants.Attributes.IsPassive) ?? authnRequest.IsPassive;
        authnRequest.AssertionConsumerServiceIndex = source.GetIntAttribute(SamlConstants.Attributes.AssertionConsumerServiceIndex);
        authnRequest.AssertionConsumerServiceUrl = source.GetAttribute(SamlConstants.Attributes.AssertionConsumerServiceURL);
        authnRequest.ProtocolBinding = source.GetAbsoluteUriAttribute(SamlConstants.Attributes.ProtocolBinding);
        authnRequest.AttributeConsumingServiceIndex = source.GetIntAttribute(SamlConstants.Attributes.AttributeConsumingServiceIndex);
        authnRequest.ProviderName = source.GetAttribute(SamlConstants.Attributes.ProviderName);
    }
}
