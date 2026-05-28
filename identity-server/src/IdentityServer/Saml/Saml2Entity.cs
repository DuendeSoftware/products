// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml;

/// <summary>
/// A Saml2 entity, i.e. an Identity Provider or a Service Provider
/// </summary>
public class Saml2Entity
{
    /// <summary>
    /// The entity id of the identity provider
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Use Metadata for configuration. Defaults to true.
    /// </summary>
    public bool LoadMetadata { get; set; } = true;

    /// <summary>
    /// Location of metadata. If null, the EntityId is used.
    /// </summary>
    public string? MetadataLocation { get; set; }

    /// <summary>
    /// Allowed algorithms if validating signatures.
    /// </summary>
    public IEnumerable<string> AllowedAlgorithms { get; set; }
        = SamlConstants.DefaultAllowedAlgorithms;

    /// <summary>
    /// Signing keys of the entity.
    /// </summary>
    public IEnumerable<SigningKey>? SigningKeys { get; set; }
}
