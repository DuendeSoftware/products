// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlWriter
{
    /// <summary>
    /// Append a KeyInfo element as a child node.
    /// </summary>
    protected virtual void AppendKeyInfo(XmlNode node, KeyInfo keyInfo)
    {
        var keyInfoElement = AppendElement(node, SamlConstants.Namespaces.XmlSignaturePrefix, SamlConstants.Elements.KeyInfo);

        foreach (var clause in keyInfo)
        {
            if (clause is KeyInfoX509Data x509Data)
            {
                AppendX509Data(keyInfoElement, x509Data);
            }
        }
    }

    /// <summary>
    /// Append an X509Data element as a child node.
    /// </summary>
    protected virtual void AppendX509Data(XmlNode node, KeyInfoX509Data x509Data)
    {
        var x509DataElement = AppendElement(node, SamlConstants.Namespaces.XmlSignaturePrefix, "X509Data");

        foreach (var cert in x509Data.Certificates)
        {
            if (cert is X509Certificate2 x509Cert)
            {
                var certElement = AppendElement(x509DataElement, SamlConstants.Namespaces.XmlSignaturePrefix, "X509Certificate");
                certElement.InnerText = Convert.ToBase64String(x509Cert.RawData);
            }
        }
    }
}
