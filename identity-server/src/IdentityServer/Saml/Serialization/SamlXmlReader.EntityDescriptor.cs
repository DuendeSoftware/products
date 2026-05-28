// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Metadata;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <inheritdoc/>
    public Task<EntityDescriptor> ReadEntityDescriptorAsync(
        XmlTraverser source,
        Ct ct) =>
        ReadEntityDescriptorInternalAsync(source, errorInspector: null, ct);

    /// <inheritdoc/>
    public Task<EntityDescriptor> ReadEntityDescriptorAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<EntityDescriptor>> errorInspector,
        Ct ct) =>
        ReadEntityDescriptorInternalAsync(source, errorInspector, ct);

    private Task<EntityDescriptor> ReadEntityDescriptorInternalAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<EntityDescriptor>>? errorInspector,
        Ct ct)
    {
        EntityDescriptor entityDescriptor = default!;

        if (source.EnsureName(SamlConstants.Elements.EntityDescriptor, SamlConstants.Namespaces.Metadata))
        {
            entityDescriptor = ReadEntityDescriptorCore(source);
        }

        source.MoveNext(true);

        CallErrorInspector(errorInspector, entityDescriptor, source);

        ThrowOnErrors(source);

        return Task.FromResult(entityDescriptor);
    }

    /// <summary>
    /// Read an EntityDescriptor
    /// </summary>
    /// <param name="source">Source data</param>
    /// <returns>EntityDescriptor</returns>
    protected EntityDescriptor ReadEntityDescriptorCore(XmlTraverser source)
    {
        var entityDescriptor = Create<EntityDescriptor>();

        ReadAttributes(source, entityDescriptor);
        ReadElements(source.GetChildren(), entityDescriptor);

        return entityDescriptor;
    }

    /// <summary>
    /// Read attributes of EntityDescriptor
    /// </summary>
    /// <param name="source">Source data</param>
    /// <param name="entityDescriptor">EntityDescriptor</param>
    protected virtual void ReadAttributes(XmlTraverser source, EntityDescriptor entityDescriptor)
    {
        entityDescriptor.EntityId = source.GetRequiredAbsoluteUriAttribute(SamlConstants.Attributes.entityID) ?? "";
        entityDescriptor.Id = source.GetAttribute(SamlConstants.Attributes.ID);
        entityDescriptor.CacheDuration = source.GetTimeSpanAttribute(SamlConstants.Attributes.cacheDuration);
        entityDescriptor.ValidUntil = source.GetDateTimeAttribute(SamlConstants.Attributes.validUntil);
    }

    /// <summary>
    /// Read the child elements of the EntityDescriptor.
    /// </summary>
    /// <param name="source">Source data</param>
    /// <param name="entityDescriptor">Entity Descriptor to populate</param>
    protected virtual void ReadElements(XmlTraverser source, EntityDescriptor entityDescriptor)
    {
        source.MoveNext();

        if (source.ReadAndValidateOptionalSignature(
            TrustedSigningKeys, AllowedAlgorithms))
        {
            entityDescriptor.TrustLevel = source.TrustLevel;
            source.MoveNext();
        }

        if (source.HasName(SamlConstants.Elements.Extensions, SamlConstants.Namespaces.Metadata))
        {
            entityDescriptor.Extensions = ReadExtensions(source);
            source.MoveNext();
        }

        // Now we're at the actual role descriptors - or possibly an AffiliationDescriptor.
        var wasRoleDescriptor = true; // Assume the best.
        do
        {
            if (source.EnsureNamespace(SamlConstants.Namespaces.Metadata))
            {
                switch (source.CurrentNode?.LocalName)
                {
                    case SamlConstants.Elements.RoleDescriptor:
                        entityDescriptor.RoleDescriptors.Add(ReadRoleDescriptor(source));
                        break;
                    case SamlConstants.Elements.IDPSSODescriptor:
                        entityDescriptor.RoleDescriptors.Add(ReadIDPSSODescriptor(source));
                        break;
                    case SamlConstants.Elements.SPSSODescriptor:
                    case SamlConstants.Elements.AuthnAuthorityDescriptor:
                    case SamlConstants.Elements.AttributeAuthorityDescriptor:
                    case SamlConstants.Elements.PDPDescriptor:
                        source.IgnoreChildren();
                        break;
                    default:
                        wasRoleDescriptor = false; // Nope, something else.
                        break;
                }
            }
        } while (wasRoleDescriptor && source.MoveNext(true));

        // There can be more data after the role descriptors that we currently do not support, skip them.
        source.Skip();
    }
}
