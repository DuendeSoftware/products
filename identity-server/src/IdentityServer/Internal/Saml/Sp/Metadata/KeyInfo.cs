// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Duende.IdentityServer.Internal.Saml.Sp.Tokens;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal abstract class KeyData
    {
    }

    internal class X509Digest
    {
        public Uri Algorithm { get; set; }
        public byte[] Value { get; set; }
    }

    internal class X509IssuerSerial
    {
        public string Name { get; private set; }
        public string Serial { get; private set; }

        public X509IssuerSerial(string name, string serial)
        {
            Name = name;
            Serial = serial;
        }
    }

    internal class X509Data : KeyData
    {
        public X509IssuerSerial IssuerSerial { get; set; }
        public byte[] SKI { get; set; }
        public string SubjectName { get; set; }
        public ICollection<X509Certificate2> Certificates { get; set; } =
            new Collection<X509Certificate2>();
        public byte[] CRL { get; set; }
        public X509Digest Digest { get; set; }
    }

    internal class RetrievalMethod
    {
        public Uri Uri { get; set; }
        public Uri Type { get; set; }
        public ICollection<XmlElement> Transforms { get; private set; } =
            new Collection<XmlElement>();
    }

    internal abstract class KeyValue
    {
    }

    internal class DsaKeyValue : KeyValue
    {
        public DSAParameters Parameters { get; set; }

        public DsaKeyValue(DSAParameters parameters)
        {
            Parameters = parameters;
        }
    }

    internal class RsaKeyValue : KeyValue
    {
        public RSAParameters Parameters { get; set; }

        public RsaKeyValue(RSAParameters parameters)
        {
            Parameters = parameters;
        }
    }

    internal class EcKeyValue : KeyValue
    {
        public ECParameters Parameters { get; set; }

        public EcKeyValue(ECParameters parameters)
        {
            Parameters = parameters;
        }
    }

    internal class DSigKeyInfo
    {
        public string Id { get; set; }
        public ICollection<string> KeyNames { get; private set; } =
            new Collection<string>();
        public ICollection<KeyValue> KeyValues { get; private set; } =
            new Collection<KeyValue>();
        public ICollection<RetrievalMethod> RetrievalMethods { get; private set; } =
            new Collection<RetrievalMethod>();
        public ICollection<KeyData> Data { get; private set; } =
            new Collection<KeyData>();

        public SecurityKeyIdentifier MakeSecurityKeyIdentifier()
        {
            var ski = new SecurityKeyIdentifier();
            foreach (var keyValue in KeyValues)
            {
                if (keyValue is RsaKeyValue rsaKeyValue)
                {
                    ski.Add(new RsaKeyIdentifierClause(rsaKeyValue.Parameters));
                }
                else if (keyValue is DsaKeyValue dsaKeyValue)
                {
                    ski.Add(new DsaKeyIdentifierClause(dsaKeyValue.Parameters));
                }
                else if (keyValue is EcKeyValue ecKeyValue)
                {
                    ski.Add(new EcKeyIdentifierClause(ecKeyValue.Parameters));
                }
            }
            foreach (string keyName in KeyNames)
            {
                ski.Add(new KeyNameIdentifierClause(keyName));
            }
            foreach (var keyData in Data)
            {
                if (keyData is X509Data x509Data)
                {
                    foreach (var cert in x509Data.Certificates)
                    {
                        ski.Add(new X509RawDataKeyIdentifierClause(cert));
                    }
                    if (x509Data.IssuerSerial != null)
                    {
                        ski.Add(new X509IssuerSerialKeyIdentifierClause(
                            x509Data.IssuerSerial.Name, x509Data.IssuerSerial.Serial));
                    }
                }
            }
            return ski;
        }
    }
}
