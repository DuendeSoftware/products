// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Duende.IdentityServer.Saml.Metadata;
using Duende.IdentityServer.Saml.Samlp;

namespace Duende.IdentityServer.Saml.Serialization;

/// <summary>
/// Write Saml entities to XML
/// </summary>
public interface ISamlXmlWriter
{
    /// <summary>
    /// Certificate used to sign assertions. When set, assertions written by
    /// <see cref="Write(Response)"/> will be signed with this certificate.
    /// </summary>
    X509Certificate2? AssertionSigningCertificate { get; set; }

    /// <summary>
    /// Create an Xml document and write an AuthnRequest to it.
    /// </summary>
    /// <param name="authnRequest">AuthnRequest</param>
    /// <returns>Created XmlDoc</returns>
    XmlDocument Write(AuthnRequest authnRequest);

    /// <summary>
    /// Create an Xml document and write a SamlResponse to it.
    /// If <see cref="AssertionSigningCertificate"/> is set, assertions will be signed.
    /// </summary>
    /// <param name="response">Saml Response</param>
    /// <returns>Created XmlDoc</returns>
    XmlDocument Write(Response response);

    /// <summary>
    /// Create an Xml document and write an EtnityDescriptor to it.
    /// </summary>
    /// <param name="entityDescriptor">Entity Descriptor</param>
    /// <returns>Created XmlDoc</returns>
    XmlDocument Write(EntityDescriptor entityDescriptor);

    /// <summary>
    /// Create an Xml document and write a LogoutResponse to it.
    /// </summary>
    /// <param name="logoutResponse">Logout Response</param>
    /// <returns>Created XmlDoc</returns>
    XmlDocument Write(LogoutResponse logoutResponse);

    /// <summary>
    /// Create an Xml document and write a LogoutRequest to it.
    /// </summary>
    /// <param name="logoutRequest">Logout Request</param>
    /// <returns>Created XmlDoc</returns>
    XmlDocument Write(LogoutRequest logoutRequest);
}
