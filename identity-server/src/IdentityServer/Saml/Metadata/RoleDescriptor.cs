// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Saml.Metadata;

/// <summary>
/// Base class for role descriptors, implements RoleDescriptorType
/// </summary>
public class RoleDescriptor
{
    /// <summary>
    /// ProtocolSupportEnumeration attribute value. Defaults to <see cref="SamlConstants.Namespaces.Protocol"/>
    /// </summary>
    public string ProtocolSupportEnumeration { get; set; }
        = SamlConstants.Namespaces.Protocol;

    /// <summary>
    /// Cryptographic keys for signing and encryption.
    /// </summary>
    public List<KeyDescriptor> Keys { get; set; } = [];
}
