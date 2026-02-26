// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.Metadata.Models;

namespace Duende.IdentityServer.Internal.Saml.Metadata;

/// <summary>
/// Serializes SAML metadata EntityDescriptor to XML.
/// </summary>
internal static class EntityDescriptorSerializer
{
    private static readonly XNamespace MdNamespace = SamlConstants.Namespaces.Metadata;
    private static readonly XNamespace DsNamespace = SamlConstants.Namespaces.XmlSignature;

    /// <summary>
    /// Serializes an EntityDescriptor to SAML metadata XML string.
    /// </summary>
    /// <param name="descriptor">The entity descriptor to serialize.</param>
    /// <returns>XML string representing the SAML metadata.</returns>
    internal static XDocument SerializeToXml(EntityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var root = new XElement(MdNamespace + SamlConstants.MetadataElements.EntityDescriptor,
            new XAttribute(SamlConstants.MetadataAttributes.EntityId, descriptor.EntityId));

        // Add validUntil if specified
        if (descriptor.ValidUntil.HasValue)
        {
            root.Add(new XAttribute(SamlConstants.MetadataAttributes.ValidUntil,
                descriptor.ValidUntil.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)));
        }

        // Add IDPSSODescriptor if present
        if (descriptor.IdpSsoDescriptor != null)
        {
            root.Add(SerializeIdpSsoDescriptor(descriptor.IdpSsoDescriptor));
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            root);
    }

    private static XElement SerializeIdpSsoDescriptor(IdpSsoDescriptor descriptor)
    {
        var element = new XElement(MdNamespace + SamlConstants.MetadataElements.IdpSsoDescriptor,
            new XAttribute(SamlConstants.MetadataAttributes.ProtocolSupportEnumeration,
                descriptor.ProtocolSupportEnumeration));

        // Add WantAuthnRequestsSigned if true
        if (descriptor.WantAuthnRequestsSigned)
        {
            element.Add(new XAttribute(SamlConstants.MetadataAttributes.WantAuthnRequestsSigned, "true"));
        }

        // Add KeyDescriptors
        foreach (var keyDescriptor in descriptor.KeyDescriptors)
        {
            element.Add(SerializeKeyDescriptor(keyDescriptor));
        }

        // Add NameIDFormats
        foreach (var nameIdFormat in descriptor.NameIdFormats)
        {
            element.Add(new XElement(MdNamespace + SamlConstants.MetadataElements.NameIdFormat, nameIdFormat));
        }

        // Add SingleSignOnServices
        foreach (var ssoService in descriptor.SingleSignOnServices)
        {
            element.Add(SerializeSingleSignOnService(ssoService));
        }

        // Add SingleLogoutServices
        foreach (var sloService in descriptor.SingleLogoutServices)
        {
            element.Add(SerializeSingleLogoutService(sloService));
        }

        return element;
    }

    private static XElement SerializeKeyDescriptor(KeyDescriptor descriptor)
    {
        var element = new XElement(MdNamespace + SamlConstants.MetadataElements.KeyDescriptor);

        // Add use attribute if specified
        if (descriptor.Use.HasValue)
        {
            element.Add(new XAttribute(SamlConstants.MetadataAttributes.Use, SamlConstants.MetadataAttributes.ToString(descriptor.Use.Value)));
        }

        // Add KeyInfo with X509Data
        var keyInfo = new XElement(DsNamespace + SamlConstants.MetadataElements.KeyInfo,
            new XElement(DsNamespace + SamlConstants.MetadataElements.X509Data,
                new XElement(DsNamespace + SamlConstants.MetadataElements.X509Certificate,
                    descriptor.X509Certificate)));

        element.Add(keyInfo);

        return element;
    }

    private static XElement SerializeSingleSignOnService(SingleSignOnService service) => new(MdNamespace + SamlConstants.MetadataElements.SingleSignOnService,
            new XAttribute(SamlConstants.MetadataAttributes.Binding, service.Binding.ToUrn()),
            new XAttribute(SamlConstants.MetadataAttributes.Location, service.Location));

    private static XElement SerializeSingleLogoutService(SingleLogoutService service) => new(MdNamespace + SamlConstants.MetadataElements.SingleLogoutService,
            new XAttribute(SamlConstants.MetadataAttributes.Binding, service.Binding.ToUrn()),
            new XAttribute(SamlConstants.MetadataAttributes.Location, service.Location));
}
