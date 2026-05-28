// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Metadata;

/// <summary>
/// Abstract SSODescriptor type.
/// </summary>
public abstract class SSODescriptor : RoleDescriptor
{
    /// <summary>
    /// Artifact resolution services.
    /// </summary>
    public List<IndexedEndpoint> ArtifactResolutionServices { get; } = [];

    /// <summary>
    /// Single logout services.
    /// </summary>
    public List<Endpoint> SingleLogoutServices { get; } = [];

    /// <summary>
    /// Supported NameID formats.
    /// </summary>
    public List<string> NameIdFormats { get; } = [];
}
