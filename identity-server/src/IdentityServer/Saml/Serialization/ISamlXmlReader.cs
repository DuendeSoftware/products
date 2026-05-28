// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Metadata;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

/// <summary>
/// Reader for Saml classes from Xml
/// </summary>
public interface ISamlXmlReader
{
    /// <summary>
    /// Allowed hash algorithms if validating signatures. Values should be e.g.
    /// "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256" which is compared to
    /// the algorithm identifier Url.
    /// </summary>
    IEnumerable<string>? AllowedAlgorithms { get; set; }

    /// <summary>
    /// Signing keys to trust when validating signatures of the metadata. In addition
    /// to these, the signing keys configured for a known issuer are considered. This
    /// property is mostly useful for validation of signed metadata.
    /// </summary>
    IEnumerable<SigningKey>? TrustedSigningKeys { get; set; }

    /// <summary>
    /// Called when information about a Saml entity is needed, e.g. to get the signing
    /// keys configured for an entity.
    /// </summary>
    Func<string, Ct, Task<Saml2Entity?>>? EntityResolver { get; set; }

    /// <summary>
    /// Read an Entity Descriptor
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>EntityDescriptor</returns>
    Task<EntityDescriptor> ReadEntityDescriptorAsync(
        XmlTraverser source,
        Ct ct);

    /// <summary>
    /// Read an Entity Descriptor
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="errorInspector">Callback that can inspect and alter errors before throwing</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>EntityDescriptor</returns>
    Task<EntityDescriptor> ReadEntityDescriptorAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<EntityDescriptor>> errorInspector,
        Ct ct);

    /// <summary>
    /// Read a Saml response
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>SamlResponse</returns>
    Task<Response> ReadResponseAsync(
        XmlTraverser source,
        Ct ct);

    /// <summary>
    /// Read a Saml response
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="errorInspector">Callback that can inspect and alter errors before throwing</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>SamlResponse</returns>
    Task<Response> ReadResponseAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<Response>> errorInspector,
        Ct ct);

    /// <summary>
    /// Read an <see cref="Assertion"/>
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns><see cref="Assertion"/></returns>
    Task<Assertion> ReadAssertionAsync(
        XmlTraverser source,
        Ct ct);

    /// <summary>
    /// Read an <see cref="Assertion"/>
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="errorInspector">Callback that can inspect and alter errors before throwing</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns><see cref="Assertion"/></returns>
    Task<Assertion> ReadAssertionAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<Assertion>> errorInspector,
        Ct ct);

    /// <summary>
    /// Read an <see cref="AuthnRequest"/>
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="errorInspector">Optional callback that can inspect and alter errors before throwing</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns><see cref="AuthnRequest"/></returns>
    Task<AuthnRequest> ReadAuthnRequestAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<AuthnRequest>>? errorInspector,
        Ct ct);

    /// <summary>
    /// Read a <see cref="LogoutRequest"/>
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="errorInspector">Optional callback that can inspect and alter errors before throwing</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns><see cref="LogoutRequest"/></returns>
    Task<LogoutRequest> ReadLogoutRequestAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<LogoutRequest>>? errorInspector,
        Ct ct);

    /// <summary>
    /// Read a <see cref="LogoutResponse"/>
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="errorInspector">Optional callback that can inspect and alter errors before throwing</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns><see cref="LogoutResponse"/></returns>
    Task<LogoutResponse> ReadLogoutResponseAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<LogoutResponse>>? errorInspector,
        Ct ct);
}
