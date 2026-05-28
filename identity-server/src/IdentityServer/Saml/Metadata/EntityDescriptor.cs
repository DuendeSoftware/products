// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml.Metadata;

/// <summary>
/// A Saml2 Metadata &lt;EntityDescriptor&gt; element.
/// </summary>
public class EntityDescriptor : MetadataBase
{
    /// <summary>
    /// Id of the Entity. MUST be an absolute URI
    /// </summary>
    public string EntityId { get; set; } = "";

    /// <summary>
    /// The extensions node of the metadata.
    /// </summary>
    public Common.Extensions? Extensions { get; set; }

    /// <summary>
    /// Role Descriptors.
    /// </summary>
    public List<RoleDescriptor> RoleDescriptors { get; } = [];
}
