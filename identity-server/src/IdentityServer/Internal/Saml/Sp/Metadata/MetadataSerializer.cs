// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Duende.IdentityServer.Internal.Saml.Sp.Selectors;
using Duende.IdentityServer.Internal.Saml.Sp.Tokens;
using Microsoft.IdentityModel.Tokens.Saml2;
using Microsoft.IdentityModel.Xml;
using SigningCredentials = Microsoft.IdentityModel.Tokens.SigningCredentials;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class MetadataSerializationException : Exception
    {
        public MetadataSerializationException(string message) :
            base(message)
        {
        }

        public MetadataSerializationException(string message, Exception innerException) :
            base(message, innerException)
        {
        }
    }

    internal class MetadataSerializer
    {
        const string XmlNs = "http://www.w3.org/XML/1998/namespace";
        const string FedNs = "http://docs.oasis-open.org/wsfed/federation/200706";
        const string WsaNs = "http://www.w3.org/2005/08/addressing";
        const string WspNs = "http://schemas.xmlsoap.org/ws/2002/12/policy";
        const string Saml2MetadataNs = "urn:oasis:names:tc:SAML:2.0:metadata";
        const string Saml2AssertionNs = "urn:oasis:names:tc:SAML:2.0:assertion";
        const string XsiNs = "http://www.w3.org/2001/XMLSchema-instance";
        const string AuthNs = "http://docs.oasis-open.org/wsfed/authorization/200706";
        const string XEncNs = "http://www.w3.org/2001/04/xmlenc#";
        const string DSigNs = "http://www.w3.org/2000/09/xmldsig#";
        const string EcDsaNs = "http://www.w3.org/2001/04/xmldsig-more#";
        const string DSig11Ns = "http://www.w3.org/2009/xmldsig11#";
        const string IdpDiscNs = "urn:oasis:names:tc:SAML:profiles:SSO:idp-discovery-protocol";

        public SecurityTokenSerializer SecurityTokenSerializer { get; private set; }

        public MetadataSerializer(SecurityTokenSerializer serializer)
        {
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }
            SecurityTokenSerializer = serializer;
        }

        public MetadataSerializer()
        {
        }

        string GetAttribute(XmlReader reader, string att, string def)
        {
            string value = reader.GetAttribute(att);
            return string.IsNullOrEmpty(value) ? def : value;
        }

        protected virtual AdditionalMetadataLocation CreateAdditionalMetadataLocationInstance() =>
            new AdditionalMetadataLocation();

        protected virtual AffiliationDescriptor CreateAffiliationDescriptorInstance() =>
            new AffiliationDescriptor();

        protected virtual ApplicationServiceEndpoint CreateApplicationServiceEndpointInstance() =>
            new ApplicationServiceEndpoint();

        protected virtual ApplicationServiceDescriptor CreateApplicationServiceInstance() =>
            new ApplicationServiceDescriptor();

        protected virtual AssertionConsumerService CreateAssertionConsumerServiceInstance() =>
            new AssertionConsumerService();

        protected virtual AssertionIdRequestService CreateAssertionIdRequestServiceInstance() =>
            new AssertionIdRequestService();

        protected virtual AttributeAuthorityDescriptor CreateAttributeAuthorityDescriptorInstance() =>
            new AttributeAuthorityDescriptor();

        protected virtual AttributeConsumingService CreateAttributeConsumingServiceInstance() =>
            new AttributeConsumingService();

        protected virtual AttributeService CreateAttributeServiceInstance() =>
            new AttributeService();

        protected virtual AttributeProfile CreateAttributeProfileInstance() =>
            new AttributeProfile();

        protected virtual AuthnAuthorityDescriptor CreateAuthnAuthorityDescriptorInstance() =>
            new AuthnAuthorityDescriptor();

        protected virtual AuthnQueryService CreateAuthnQueryServiceInstance() =>
            new AuthnQueryService();

        protected virtual AuthzService CreateAuthzServiceInstance() =>
            new AuthzService();

        protected virtual CipherData CreateCipherDataInstance() =>
            new CipherData();

        protected virtual ClaimValue CreateClaimValueInstance() =>
            new ClaimValue();

        protected virtual CipherReference CreateCipherReferenceInstance() =>
            new CipherReference();

        protected virtual ConstrainedValue CreateConstrainedValueInstance() =>
            new ConstrainedValue();

        protected virtual ContactPerson CreateContactPersonInstance() =>
            new ContactPerson();

        protected DiscoveryResponse CreateDiscoveryResponseInstance() =>
            new DiscoveryResponse();

        protected virtual DSigKeyInfo CreateDSigKeyInfoInstance() =>
            new DSigKeyInfo();

        protected virtual EndpointReference CreateEndpointReferenceInstance() =>
            new EndpointReference();

        protected virtual PDPDescriptor CreatePDPDescriptorInstance() =>
            new PDPDescriptor();

        protected virtual EncryptedData CreateEncryptedDataInstance() =>
            new EncryptedData();

        protected virtual EncryptionProperty CreateEncryptionPropertyInstance() =>
            new EncryptionProperty();

        protected virtual EncryptionProperties CreateEncryptionPropertiesInstance() =>
            new EncryptionProperties();

        protected virtual EncryptedValue CreateEncryptedValueInstance() =>
            new EncryptedValue();

        protected virtual EncryptionMethod CreateEncryptionMethodInstance() =>
            new EncryptionMethod();

        protected virtual Endpoint CreateEndpointInstance() =>
            new Endpoint();

        protected virtual EntitiesDescriptor CreateEntitiesDescriptorInstance() =>
            new EntitiesDescriptor();

        protected virtual EntityDescriptor CreateEntityDescriptorInstance() =>
            new EntityDescriptor();

        protected virtual IdpSsoDescriptor CreateIdpSsoDescriptorInstance() =>
            new IdpSsoDescriptor();

        protected virtual KeyDescriptor CreateKeyDescriptorInstance() =>
            new KeyDescriptor();

        protected virtual LocalizedName CreateLocalizedNameInstance() =>
            new LocalizedName();

        protected virtual LocalizedUri CreateLocalizedUriInstance() =>
            new LocalizedUri();

        protected virtual ManageNameIDService CreateManageNameIDServiceInstance() =>
            new ManageNameIDService();

        protected virtual NameIDFormat CreateNameIDFormatInstance() =>
            new NameIDFormat();

        protected virtual NameIDMappingService CreateNameIDMappingServiceInstance() =>
            new NameIDMappingService();

        protected virtual Organization CreateOrganizationInstance() =>
            new Organization();

        protected virtual PassiveRequestorEndpoint CreatePassiveRequestorEndpointInstance() =>
            new PassiveRequestorEndpoint();

        protected virtual RequestedAttribute CreateRequestedAttributeInstance(string name) =>
            new RequestedAttribute(name);

        protected virtual Saml2Attribute CreateSaml2AttributeInstance(string name) =>
            new Saml2Attribute(name);

        protected virtual ServiceName CreateServiceNameInstance() =>
            new ServiceName();

        protected virtual SingleLogoutService CreateSingleLogoutServiceInstance() =>
            new SingleLogoutService();

        protected virtual SingleSignOutNotificationEndpoint CreateSingleSignOutNotificationEndpointInstance() =>
            new SingleSignOutNotificationEndpoint();

        protected virtual SecurityTokenServiceDescriptor CreateSecurityTokenServiceDescriptorInstance() =>
            new SecurityTokenServiceDescriptor();

        protected virtual SingleSignOnService CreateSingleSignOnServiceInstance() =>
            new SingleSignOnService();

        protected virtual SpSsoDescriptor CreateSpSsoDescriptorInstance() =>
            new SpSsoDescriptor();

        protected virtual XEncEncryptionMethod CreateXEncEncryptionMethodInstance() =>
            new XEncEncryptionMethod();

        // <complexType name="ApplicationServiceType">
        //   <extension base="fed:WebServiceDescriptorType">
        // 	   <sequence>
        //       <element ref="fed:ApplicationServiceEndpoint" minOccurs="1" maxOccurs="unbounded"/>
        //       <element ref="fed:SingleSignOutNotificationEndpoint" minOccurs="0" maxOccurs="unbounded"/>
        //       <element ref="fed:PassiveRequestorEndpoint" minOccurs="0" maxOccurs="unbounded"/>
        //     </sequence>
        //   </extension>
        // </complexType>
        // 
        // <element name="fed:ApplicationServiceEndpoint" type="tns:EndpointType"/>
        // <element name="fed:SingleSignOutNotificationEndpoint" type="tns:EndpointType"/>
        // <element name="fed:PassiveRequestorEndpoint" type="tns:EndpointType"/>
        protected virtual ApplicationServiceDescriptor ReadApplicationServiceDescriptor(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var descriptor = CreateApplicationServiceInstance();
            ReadWebServiceDescriptorAttributes(reader, descriptor);
            ReadCustomAttributes(reader, descriptor);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("ApplicationServiceEndpoint", FedNs))
                {
                    descriptor.Endpoints.Add(ReadWrappedEndpointReference(reader));
                }
                else if (reader.IsStartElement("SingleSignOutNotificationEndpoint", FedNs))
                {
                    descriptor.SingleSignOutEndpoints.Add(ReadWrappedEndpointReference(reader));
                }
                else if (reader.IsStartElement("PassiveRequestorEndpoint", FedNs))
                {
                    descriptor.PassiveRequestorEndpoints.Add(ReadWrappedEndpointReference(reader));
                }
                else if (ReadWebServiceDescriptorElement(reader, descriptor))
                {
                }
                else
                {
                    return ReadCustomElement(reader, descriptor);
                }
                return true; // handled above
            });

            if (descriptor.Endpoints.Count == 0)
            {
                throw new MetadataSerializationException("ApplicationServiceType extension with no ApplicationServiceEndpoints");
            }
            return descriptor;
        }

        // <element name="ContactPerson" type="md:ContactType"/>
        //   <complexType name="ContactType">
        //     <sequence>
        //       <element ref="md:Extensions" minOccurs="0"/>
        //       <element ref="md:Company" minOccurs="0"/>
        //       <element ref="md:GivenName" minOccurs="0"/>
        //       <element ref="md:SurName" minOccurs="0"/>
        //       <element ref="md:EmailAddress" minOccurs="0" maxOccurs="unbounded"/>
        //       <element ref="md:TelephoneNumber" minOccurs="0" maxOccurs="unbounded"/>
        //     </sequence>
        //     <attribute name="contactType" type="md:ContactTypeType" use="required"/>
        //     <anyAttribute namespace="##other" processContents="lax"/>
        //   </complexType>
        //   <element name="Company" type="string"/>
        //   <element name="GivenName" type="string"/>
        //   <element name="SurName" type="string"/>
        //   <element name="EmailAddress" type="anyURI"/>
        //   <element name="TelephoneNumber" type="string"/>
        //   <simpleType name="ContactTypeType">
        //     <restriction base="string">
        //       <enumeration value="technical"/>
        //       <enumeration value="support"/>
        //       <enumeration value="administrative"/>
        //       <enumeration value="billing"/>
        //       <enumeration value="other"/>
        //     </restriction>
        //   </simpleType>
        protected virtual ContactPerson ReadContactPerson(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            ContactPerson person = CreateContactPersonInstance();
            string contactType = reader.GetAttribute("contactType");
            try
            {
                person.Type = ContactTypeHelpers.Parse(contactType);
            }
            catch (FormatException)
            {
                throw new MetadataSerializationException(
                    $"ContactPerson with unknown contactType attribute '{contactType}'");
            }
            ReadCustomAttributes(reader, person);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("Company", Saml2MetadataNs))
                {
                    person.Company = ReadTrimmedString(reader);
                }
                else if (reader.IsStartElement("GivenName", Saml2MetadataNs))
                {
                    person.GivenName = ReadTrimmedString(reader);
                }
                else if (reader.IsStartElement("SurName", Saml2MetadataNs))
                {
                    person.Surname = ReadTrimmedString(reader);
                }
                else if (reader.IsStartElement("EmailAddress", Saml2MetadataNs))
                {
                    person.EmailAddresses.Add(ReadTrimmedString(reader));
                }
                else if (reader.IsStartElement("TelephoneNumber", Saml2MetadataNs))
                {
                    person.TelephoneNumbers.Add(ReadTrimmedString(reader));
                }
                else if (reader.IsStartElement("Extensions", Saml2MetadataNs))
                {
                    ReadChildrenAsXmlElements(reader, person.Extensions.Add);
                }
                else
                {
                    return ReadCustomElement(reader, person);
                }
                return true; // handled above
            });

            return person;
        }

        protected virtual void ReadCustomAttributes<T>(XmlReader reader, T target)
        {
        }

        protected virtual bool ReadCustomElement<T>(XmlReader reader, T target)
        {
            return false;
        }

        protected virtual KeyValue ReadCustomKeyValue(XmlReader reader) => null;


        protected virtual void ReadCustomRoleDescriptor(string xsiType, XmlReader reader, EntityDescriptor entityDescriptor)
        {
            reader.Skip();
        }

        protected byte[] ReadBase64(XmlReader reader)
        {
            /* ReadElementContentAsBase64 throws a NotSupportedException
			byte[] buf = new byte[65536];
			using (var ms = new MemoryStream())
			{
				for (; ; )
				{
					int read = reader.ReadElementContentAsBase64(buf, 0, buf.Length);
					if (read == 0)
					{
						return ms.ToArray();
					}
					ms.Write(buf, 0, read);
				}
			}*/
            return Convert.FromBase64String(reader.ReadElementContentAsString());
        }

        // <element name="DSAKeyValue" type="ds:DSAKeyValueType" /> 
        // 
        // <complexType name="DSAKeyValueType"> 
        //   <sequence>
        //     <sequence minOccurs="0">
        //       <element name="P" type="ds:CryptoBinary"/> 
        //       <element name="Q" type="ds:CryptoBinary"/>
        //     </sequence>
        //     <element name="G" type="ds:CryptoBinary" minOccurs="0"/> 
        //     <element name="Y" type="ds:CryptoBinary"/> 
        //     <element name="J" type="ds:CryptoBinary" minOccurs="0"/>
        //     <sequence minOccurs="0">
        //       <element name="Seed" type="ds:CryptoBinary"/> 
        //       <element name="PgenCounter" type="ds:CryptoBinary"/> 
        //     </sequence>
        //   </sequence>
        // </complexType>
        protected virtual DsaKeyValue ReadDSAKeyValue(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var parameters = new DSAParameters();
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("P", DSigNs))
                {
                    parameters.P = ReadBase64(reader);
                }
                else if (reader.IsStartElement("Q", DSigNs))
                {
                    parameters.Q = ReadBase64(reader);
                }
                else if (reader.IsStartElement("G", DSigNs))
                {
                    parameters.G = ReadBase64(reader);
                }
                else if (reader.IsStartElement("Y", DSigNs))
                {
                    parameters.Y = ReadBase64(reader);
                }
                else if (reader.IsStartElement("J", DSigNs))
                {
                    parameters.J = ReadBase64(reader);
                }
                else if (reader.IsStartElement("Seed", DSigNs))
                {
                    parameters.Seed = ReadBase64(reader);
                }
                else if (reader.IsStartElement("PgenCounter", DSigNs))
                {
                    byte[] counter = ReadBase64(reader);
                    // big endian
                    parameters.Counter = (counter[0] << 24) | (counter[1] << 16) | (counter[2] << 8) | counter[3];
                }
                else
                {
                    throw new MetadataSerializationException(
                        $"Unknown DSAKeyValue parameter <{reader.Name} xmlns='{reader.NamespaceURI}'>");
                }
                return true;
            });
            if (parameters.P == null || parameters.Q == null || parameters.Y == null)
            {
                throw new MetadataSerializationException(
                    "DSAKeyValue is missing one of the mandatory parameters (P, Q, Y)");
            }
            return new DsaKeyValue(parameters);
        }

        // <element name="RSAKeyValue" type="ds:RSAKeyValueType" />
        // 
        // <complexType name="RSAKeyValueType">
        //   <sequence>
        //     <element name="Modulus" type="ds:CryptoBinary" /> 
        //     <element name="Exponent" type="ds:CryptoBinary" />
        //   </sequence>
        // </complexType>
        protected virtual RsaKeyValue ReadRSAKeyValue(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var parameters = new RSAParameters();
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("Modulus", DSigNs))
                {
                    parameters.Modulus = ReadBase64(reader);
                }
                else if (reader.IsStartElement("Exponent", DSigNs))
                {
                    parameters.Exponent = ReadBase64(reader);
                }
                else
                {
                    throw new MetadataSerializationException(
                        $"Unknown DSAKeyValue parameter {reader.Name}");
                }
                return true;
            });
            if (parameters.Modulus == null || parameters.Exponent == null)
            {
                throw new MetadataSerializationException(
                    "RSAKeyValue is missing one of the mandatory parameters (Modulus, Exponent)");
            }
            return new RsaKeyValue(parameters);
        }

        byte[] ParseBigIntValue(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new MetadataSerializationException(
                    $"{parameterName} parameter is missing");
            }
            BigInteger intValue;
            if (!BigInteger.TryParse(value, out intValue))
            {
                throw new MetadataSerializationException(
                    $"{parameterName} parameter is not a valid number");
            }
            return intValue.ToByteArray();
        }

        protected virtual EcKeyValue ReadECDSAKeyValue(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            ECParameters parameters = new ECParameters();
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("DomainParameters", EcDsaNs))
                {
                    ReadChildren(reader, () =>
                    {
                        if (reader.IsStartElement("NamedCurve", EcDsaNs))
                        {
                            string oid = reader.GetAttribute("URN");
                            if (string.IsNullOrEmpty(oid))
                            {
                                throw new MetadataSerializationException(
                                    $"ECDSAKeyValue/NamedCurve is missing a URN attribute");
                            }
                            if (!oid.StartsWith("urn:oid:", StringComparison.Ordinal))
                            {
                                throw new MetadataSerializationException(
                                    $"Unknown EC curve type {oid}");
                            }
                            oid = oid.Substring("urn:oid:".Length);
                            parameters.Curve = ECCurve.CreateFromValue(oid);
                            reader.Skip();
                        }
                        else
                        {
                            throw new MetadataSerializationException(
                                $"Invalid child element '{reader.Name}' in ECDSAKeyValue/DomainParameters");
                        }
                        return true;
                    });
                }
                else if (reader.IsStartElement("PublicKey", EcDsaNs))
                {
                    ReadChildren(reader, () =>
                    {
                        if (reader.IsStartElement("X", EcDsaNs))
                        {
                            parameters.Q.X = ParseBigIntValue(reader.GetAttribute("Value"),
                                "ECDSAKeyValue/X/@Value");
                        }
                        else if (reader.IsStartElement("Y", EcDsaNs))
                        {
                            parameters.Q.Y = ParseBigIntValue(reader.GetAttribute("Value"),
                                "ECDSAKeyValue/Y/@Value");
                        }
                        else
                        {
                            throw new MetadataSerializationException(
                                $"Invalid child element '{reader.Name}' in ECDSAKeyValue/PublicKey");
                        }
                        reader.Skip();
                        return true;
                    });
                }
                // I can't see this is used in the wild.  Also I can't figure out
                // how to map the parameters to the .NET ECParameters object.
                // else if (reader.IsStartElement("ECParameters", EcDsaNs))
                // {
                // }
                else
                {
                    throw new MetadataSerializationException(
                        $"Unknown ECKeyValue parameter {reader.Name}");
                }
                return true;
            });
            parameters.Validate();
            return new EcKeyValue(parameters);
        }

        // <element name="ECKeyValue" type="dsig11:ECKeyValueType" />
        // 
        // <complexType name="ECKeyValueType">
        //   <sequence>
        //     <choice>
        //       <element name="ECParameters" type="dsig11:ECParametersType" />
        //       <element name="NamedCurve" type="dsig11:NamedCurveType" />
        //     </choice>
        //     <element name="PublicKey" type="dsig11:ECPointType" />
        //   </sequence>
        //   <attribute name="Id" type="ID" use="optional" />
        // </complexType>
        // 
        // <complexType name="NamedCurveType">
        //   <attribute name="URI" type="anyURI" use="required" />
        // </complexType>
        // 
        // <simpleType name="ECPointType">
        //   <restriction base="ds:CryptoBinary" />
        // </simpleType>
        protected virtual EcKeyValue ReadECKeyValue(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            ECParameters parameters = new ECParameters();
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("NamedCurve", DSig11Ns))
                {
                    string oid = reader.GetAttribute("URI");
                    if (string.IsNullOrEmpty(oid))
                    {
                        throw new MetadataSerializationException(
                            $"NamedCurve element missing URI attribute");
                    }
                    if (!oid.StartsWith("urn:oid:", StringComparison.Ordinal))
                    {
                        throw new MetadataSerializationException(
                            $"Unknown EC curve type {oid}");
                    }
                    oid = oid.Substring("urn:oid:".Length);
                    parameters.Curve = ECCurve.CreateFromValue(oid);
                    reader.Skip();
                }
                else if (reader.IsStartElement("PublicKey", DSig11Ns))
                {
                    string pkData = reader.ReadElementContentAsString();
                    byte[] pkBytes = Convert.FromBase64String(pkData);
                    if (pkBytes[0] != 4)
                    {
                        throw new MetadataSerializationException(
                            "Missing magic number in EC PublicKey data");
                    }
                    int numLen = (pkBytes.Length - 1) / 2;
                    parameters.Q.X = new ArraySegment<byte>(
                        pkBytes, 1, (pkBytes.Length - 1) / 2).ToArray();
                    parameters.Q.Y = new ArraySegment<byte>(
                        pkBytes, 1 + numLen, numLen).ToArray();
                }
                // I can't see this is used in the wild.  Also I can't figure out
                // how to map the parameters to the .NET ECParameters object.
                // else if (reader.IsStartElement("ECParameters", DSig11Ns))
                // {
                // }
                else
                {
                    throw new MetadataSerializationException(
                        $"Unknown ECKeyValue parameter {reader.Name}");
                }
                return true;

            });
            return new EcKeyValue(parameters);
        }

        // <element name = "KeyValue" type="ds:KeyValueType" /> 
        // <complexType name = "KeyValueType" mixed="true">
        //   <choice>
        //     <element ref="ds:DSAKeyValue"/>
        //     <element ref="ds:RSAKeyValue"/>
        //     <!-- <element ref="dsig11:ECKeyValue"/> -->
        //     <!-- ECC keys(XMLDsig 1.1) will use the any element -->
        //     <any namespace="##other" processContents="lax"/>
        //   </choice>
        // </complexType>
        protected virtual KeyValue ReadKeyValue(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            KeyValue value;
            if (reader.IsEmptyElement)
            {
                throw new MetadataSerializationException("KeyValue missing child key element");
            }
            reader.ReadStartElement();
            if (reader.IsStartElement("DSAKeyValue", DSigNs))
            {
                value = ReadDSAKeyValue(reader);
            }
            else if (reader.IsStartElement("RSAKeyValue", DSigNs))
            {
                value = ReadRSAKeyValue(reader);
            }
            else if (reader.IsStartElement("ECKeyValue", DSig11Ns))
            {
                value = ReadECKeyValue(reader);
            }
            else if (reader.IsStartElement("ECDSAKeyValue", EcDsaNs))
            {
                value = ReadECDSAKeyValue(reader);
            }
            else
            {
                value = ReadCustomKeyValue(reader);
            }
            if (value == null)
            {
                throw new MetadataSerializationException(
                    $"KeyValue with unknown child key element {reader.Name}");
            }
            SkipToElementEnd(reader);
            return value;
        }

        // <element name="RetrievalMethod" type="ds:RetrievalMethodType" /> 
        // 
        // <complexType name="RetrievalMethodType">
        //   <sequence>
        //     <element ref="ds:Transforms" minOccurs="0" /> 
        //   </sequence>  
        //   <attribute name="URI" type="anyURI" />
        //   <attribute name="Type" type="anyURI" use="optional" />
        // </complexType>
        protected virtual RetrievalMethod ReadRetrievalMethod(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var method = new RetrievalMethod();
            method.Type = GetUriAttribute(reader, "Type", method.Type);
            method.Uri = GetUriAttribute(reader, "URI", method.Uri);
            ReadCustomAttributes(reader, method);
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("Transforms", DSigNs))
                {
                    ReadChildrenAsXmlElements(reader, method.Transforms.Add);
                }
                else
                {
                    return ReadCustomElement(reader, method);
                }
                return true; // handled above
            });
            return method;
        }

        // <complexType name="X509IssuerSerialType"> 
        //   <sequence> 
        //     <element name="X509IssuerName" type="string"/> 
        //     <element name="X509SerialNumber" type="integer"/> 
        //   </sequence>
        // </complexType>
        protected virtual X509IssuerSerial ReadX509IssuerSerial(XmlReader reader)
        {
            string issuerName = null, serialNumber = null;
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("X509IssuerName", DSigNs))
                {
                    issuerName = ReadTrimmedString(reader);
                }
                else if (reader.IsStartElement("X509SerialNumber", DSigNs))
                {
                    serialNumber = ReadTrimmedString(reader);
                }
                else
                {
                    throw new MetadataSerializationException(
                        $"Unexpected '{reader.Name}' element in X509IssuerSerial");
                }
                return true;
            });
            if (issuerName == null || serialNumber == null)
            {
                throw new MetadataSerializationException(
                    "X509IssuerSerial with missing issuer name or serial number");
            }
            return new X509IssuerSerial(issuerName, serialNumber);
        }

        // 
        // <!-- targetNameSpace="http://www.w3.org/2009/xmldsig11#" -->
        // <element name="X509Digest" type="dsig11:X509DigestType"/>
        // 
        // <complexType name="X509DigestType">
        //   <simpleContent>
        //     <extension base="base64Binary">
        //       <attribute name="Algorithm" type="anyURI" use="required"/>
        //     </extension>
        //   </simpleContent>
        // </complexType>
        protected virtual X509Digest ReadX509Digest(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var digest = new X509Digest();
            string sv = reader.GetAttribute("Algorithm");
            if (string.IsNullOrEmpty(sv))
            {
                throw new MetadataSerializationException("X509Digest with missing Algorithm attribute");
            }
            digest.Algorithm = MakeUri(sv);
            digest.Value = ReadBase64(reader);
            return digest;
        }

        // <element name="X509Data" type="ds:X509DataType"/> 
        // 
        // <complexType name="X509DataType">
        //   <sequence maxOccurs="unbounded">
        //     <choice>
        //       <element name="X509IssuerSerial" type="ds:X509IssuerSerialType"/>
        //       <element name="X509SKI" type="base64Binary"/>
        //       <element name="X509SubjectName" type="string"/>
        //       <element name="X509Certificate" type="base64Binary"/>
        //       <element name="X509CRL" type="base64Binary"/>
        //       <!-- <element ref="dsig11:X509Digest"/> -->
        //       <!-- The X509Digest element (XMLDSig 1.1) will use the any element -->
        //       <any namespace="##other" processContents="lax"/>
        //     </choice>
        //   </sequence>
        // </complexType>
        // 
        // <!-- Note, this schema permits X509Data to be empty; this is 
        //      precluded by the text in 
        //      <a href="#sec-KeyInfo" class="sectionRef"></a> which states 
        //      that at least one element from the dsig namespace should be present 
        //      in the PGP, SPKI, and X509 structures. This is easily expressed for 
        //      the other key types, but not for X509Data because of its rich 
        //      structure. -->
        protected virtual X509Data ReadX509Data(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var data = new X509Data();
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("X509IssuerSerial", DSigNs))
                {
                    data.IssuerSerial = ReadX509IssuerSerial(reader);
                }
                else if (reader.IsStartElement("X509SKI", DSigNs))
                {
                    data.SKI = ReadBase64(reader);
                }
                else if (reader.IsStartElement("X509SubjectName", DSigNs))
                {
                    data.SubjectName = ReadTrimmedString(reader);
                }
                else if (reader.IsStartElement("X509Certificate", DSigNs))
                {
                    data.Certificates.Add(new X509Certificate2(ReadBase64(reader)));
                }
                else if (reader.IsStartElement("X509CRL", DSigNs))
                {
                    data.CRL = ReadBase64(reader);
                }
                else if (reader.IsStartElement("X509Digest", DSig11Ns))
                {
                    data.Digest = ReadX509Digest(reader);
                }
                return true;
            });
            return data;
        }

        // <element name="KeyInfo" type="ds:KeyInfoType"/> 
        // 
        // <complexType name="KeyInfoType" mixed="true">
        //   <choice maxOccurs="unbounded">     
        //     <element ref="ds:KeyName"/> 
        //     <element ref="ds:KeyValue"/> 
        //     <element ref="ds:RetrievalMethod"/> 
        //     <element ref="ds:X509Data"/> 
        //     <element ref="ds:PGPData"/> 
        //     <element ref="ds:SPKIData"/>
        //     <element ref="ds:MgmtData"/>
        //     <!-- <element ref="dsig11:DEREncodedKeyValue"/> -->
        //     <!-- DEREncodedKeyValue (XMLDsig 1.1) will use the any element -->
        //     <!-- <element ref="dsig11:KeyInfoReference"/> -->
        //     <!-- KeyInfoReference (XMLDsig 1.1) will use the any element -->
        //     <!-- <element ref="xenc:EncryptedKey"/> -->
        //     <!-- EncryptedKey (XMLEnc) will use the any element -->
        //     <!-- <element ref="xenc:Agreement"/> -->
        //     <!-- Agreement (XMLEnc) will use the any element -->
        //     <any processContents="lax" namespace="##other"/>
        //     <!-- (1,1) elements from (0,unbounded) namespaces -->
        //   </choice>
        //   <attribute name="Id" type="ID" use="optional"/>
        // </complexType>
        //
        // <element name="KeyName" type="string" />
        protected virtual DSigKeyInfo ReadDSigKeyInfo(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var info = CreateDSigKeyInfoInstance();
            info.Id = GetAttribute(reader, "Id", info.Id);
            ReadCustomAttributes(reader, info);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("KeyName", DSigNs))
                {
                    info.KeyNames.Add(ReadTrimmedString(reader));
                }
                else if (reader.IsStartElement("KeyValue", DSigNs))
                {
                    info.KeyValues.Add(ReadKeyValue(reader));
                }
                else if (reader.IsStartElement("RetrievalMethod", DSigNs))
                {
                    info.RetrievalMethods.Add(ReadRetrievalMethod(reader));
                }
                else if (reader.IsStartElement("X509Data", DSigNs))
                {
                    info.Data.Add(ReadX509Data(reader));
                }
                else
                {
                    return ReadCustomElement(reader, info);
                }
                return true; // handled above
            });

            return info;
        }

        int ReadXEncKeySize(XmlReader reader)
        {
            try
            {
                return reader.ReadElementContentAsInt();
            }
            catch (System.Xml.XmlException e)
            {
                throw new MetadataSerializationException(
                    "EncryptedMethodType element with invalid KeySize element", e);
            }
        }

        // Loosely defined in
        // http://docs.oasis-open.org/security/saml/Post2.0/sstc-saml-metadata-algsupport-v1.0-cs01.html
        // 
        protected virtual EncryptionMethod ReadEncryptionMethod(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var methodType = CreateEncryptionMethodInstance();
            string sv = reader.GetAttribute("Algorithm");
            if (string.IsNullOrEmpty(sv))
            {
                throw new MetadataSerializationException("EncryptionMethodType element is missing the required Algorithm attribute");
            }
            methodType.Algorithm = new Uri(sv);

            ReadCustomAttributes(reader, methodType);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("KeySize", XEncNs))
                {
                    methodType.KeySize = ReadXEncKeySize(reader);
                }
                else if (reader.IsStartElement("OAEPparams", XEncNs))
                {
                    methodType.OAEPparams = ReadBase64(reader);
                }
                else
                {
                    return ReadCustomElement(reader, methodType);
                }
                return true; // handled above
            });

            return methodType;
        }

        // <complexType name="EncryptionMethodType" mixed="true">
        //   <sequence>
        //     <element name="KeySize" minOccurs="0" type="xenc:KeySizeType"/>
        //     <element name="OAEPparams" minOccurs="0" type="base64Binary"/>
        //     <any namespace="##other" minOccurs="0" maxOccurs="unbounded"/>
        //   </sequence>
        //   <attribute name="Algorithm" type="anyURI" use="required"/>
        // </complexType>
        //
        // <simpleType name="KeySizeType">
        //   <restriction base="integer"/>
        // </simpleType>
        protected virtual XEncEncryptionMethod ReadXEncEncryptionMethod(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var methodType = CreateXEncEncryptionMethodInstance();
            string sv = reader.GetAttribute("Algorithm");
            if (string.IsNullOrEmpty(sv))
            {
                throw new MetadataSerializationException(
                    "xenc:EncryptionMethod element is missing the required Algorithm attribute");
            }
            methodType.Algorithm = new Uri(sv);

            ReadCustomAttributes(reader, methodType);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("KeySize", XEncNs))
                {
                    methodType.KeySize = ReadXEncKeySize(reader);
                }
                else if (reader.IsStartElement("OAEPparams", XEncNs))
                {
                    methodType.OAEPparams = ReadBase64(reader);
                }
                else
                {
                    return ReadCustomElement(reader, methodType);
                }
                return true; // handled above
            });

            return methodType;
        }

        // <element name="CipherReference" type="xenc:CipherReferenceType"/>
        // 
        // <complexType name="CipherReferenceType">
        //   <sequence>
        //     <element name="Transforms" type="xenc:TransformsType" minOccurs="0"/>
        //   </sequence>
        //   <attribute name="URI" type="anyURI" use="required"/>
        // </complexType>
        // 
        // <complexType name="TransformsType">
        //   <sequence>
        //     <element ref="ds:Transform" maxOccurs="unbounded"/> 
        //   </sequence>
        // </complexType>
        protected virtual CipherReference ReadCipherReference(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var reference = CreateCipherReferenceInstance();
            reference.Uri = GetUriAttribute(reader, "URI", reference.Uri);
            ReadCustomAttributes(reader, reference);
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("Transforms", XEncNs))
                {
                    ReadChildrenAsXmlElements(reader, reference.Transforms.Add);
                }
                else
                {
                    return ReadCustomElement(reader, reference);
                }
                return true;
            });
            return reference;
        }

        // <element name="CipherData" type="xenc:CipherDataType"/>
        // 
        // <complexType name="CipherDataType">
        //   <choice>
        //     <element name="CipherValue" type="base64Binary"/>
        //     <element ref="xenc:CipherReference"/>
        //   </choice>
        // </complexType>
        protected virtual CipherData ReadCipherData(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var data = CreateCipherDataInstance();
            ReadCustomAttributes(reader, data);
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("CipherReference", XEncNs))
                {
                    data.CipherReference = ReadCipherReference(reader);
                }
                else if (reader.IsStartElement("CipherValue", XEncNs))
                {
                    data.CipherValue = ReadTrimmedString(reader);
                }
                else
                {
                    return ReadCustomElement(reader, data);
                }
                return true; // handled above
            });
            return data;
        }

        // <element name="EncryptionProperty" type="xenc:EncryptionPropertyType"/> 
        // 
        // <complexType name="EncryptionPropertyType" mixed="true">
        //   <choice maxOccurs="unbounded">
        //     <any namespace="##other" processContents="lax"/>
        //   </choice>
        //   <attribute name="Target" type="anyURI" use="optional"/> 
        //   <attribute name="Id" type="ID" use="optional"/> 
        //   <anyAttribute namespace="http://www.w3.org/XML/1998/namespace"/>
        // </complexType>
        protected virtual EncryptionProperty ReadEncryptionProperty(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var prop = CreateEncryptionPropertyInstance();
            prop.Target = GetUriAttribute(reader, "Target", prop.Target);
            prop.Id = GetAttribute(reader, "Id", prop.Id);
            ReadChildren(reader, () => ReadCustomElement(reader, prop));
            return prop;
        }

        // <element name="EncryptionProperties" type="xenc:EncryptionPropertiesType"/> 
        // 
        // <complexType name="EncryptionPropertiesType">
        //   <sequence>
        //     <element ref="xenc:EncryptionProperty" maxOccurs="unbounded"/> 
        //   </sequence>
        //   <attribute name="Id" type="ID" use="optional"/> 
        // </complexType>
        protected virtual EncryptionProperties ReadEncryptionProperties(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var properties = CreateEncryptionPropertiesInstance();
            properties.Id = GetAttribute(reader, "Id", properties.Id);
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("EncryptionProperty", XEncNs))
                {
                    properties.Properties.Add(
                        ReadEncryptionProperty(reader));
                }
                else
                {
                    return ReadCustomElement(reader, properties);
                }
                return true; // handled above
            });

            return properties;
        }

        // <complexType name="EncryptedType" abstract="true">
        //   <sequence>
        //     <element name="EncryptionMethod" type="xenc:EncryptionMethodType" 
        //              minOccurs="0"/>
        //     <element ref="ds:KeyInfo" minOccurs="0"/>
        //     <element ref="xenc:CipherData"/>
        //     <element ref="xenc:EncryptionProperties" minOccurs="0"/>
        //   </sequence>
        //   <attribute name="Id" type="ID" use="optional"/>
        //   <attribute name="Type" type="anyURI" use="optional"/>
        //   <attribute name="MimeType" type="string" use="optional"/>
        //   <attribute name="Encoding" type="anyURI" use="optional"/> 
        // </complexType>
        //
        // <element name="EncryptedData" type="xenc:EncryptedDataType"/>
        // <complexType name="EncryptedDataType">
        //   <complexContent>
        //     <extension base="xenc:EncryptedType">
        //     </extension>
        //   </complexContent>
        // </complexType>
        protected virtual EncryptedData ReadEncryptedData(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var data = CreateEncryptedDataInstance();
            data.Id = GetAttribute(reader, "Id", data.Id);
            data.Type = GetUriAttribute(reader, "Type", data.Type);
            data.MimeType = GetAttribute(reader, "MimeType", data.MimeType);
            data.Encoding = GetUriAttribute(reader, "Encoding", data.Encoding);
            ReadCustomAttributes(reader, data);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("EncryptionMethod", XEncNs))
                {
                    data.EncryptionMethod = ReadXEncEncryptionMethod(reader);
                }
                else if (reader.IsStartElement("KeyInfo", DSigNs))
                {
                    data.KeyInfo = ReadDSigKeyInfo(reader);
                }
                else if (reader.IsStartElement("CipherData", XEncNs))
                {
                    data.CipherData = ReadCipherData(reader);
                }
                else if (reader.IsStartElement("EncryptionProperties", XEncNs))
                {
                    data.EncryptionProperties = ReadEncryptionProperties(reader);
                }
                else
                {
                    return ReadCustomElement(reader, data);
                }
                return true; // handled above
            });

            if (data.CipherData == null)
            {
                throw new MetadataSerializationException("EncryptedData without required CipherData element");
            }

            return data;
        }

        // <auth:EncryptedValue @DecryptionCondition="xs:anyURI">
        //   <xenc:EncryptedData>...</xenc:EncryptedData>
        // <auth:EncryptedValue>)
        protected virtual EncryptedValue ReadEncryptedValue(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var value = CreateEncryptedValueInstance();
            value.DecryptionCondition = GetUriAttribute(reader, "DecryptionCondition",
                value.DecryptionCondition);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("EncryptedData", XEncNs))
                {
                    value.EncryptedData = ReadEncryptedData(reader);
                }
                else
                {
                    return ReadCustomElement(reader, value);
                }
                return true; // handled above
            });

            return value;
        }

        // <auth:ConstrainedValue AssertConstraint="xs:boolean">
        //    ( <auth:ValueLessThan>
        //        (<auth:Value> xs:string </auth:Value> |
        //        <auth:StructuredValue> xs:any </auth:StructuredValue>)
        //      </auth:ValueLessThan> |
        //      <auth:ValueLessThanOrEqual>
        //        (<auth:Value> xs:string </auth:Value> |
        //        <auth:StructuredValue> xs:any </auth:StructuredValue>)
        //      </auth:ValueLessThanOrEqual> |
        //      <auth:ValueGreaterThan>
        //        (<auth:Value> xs:string </auth:Value> |
        //        <auth:StructuredValue> xs:any </auth:StructuredValue>)
        //      </auth:ValueGreaterThan> |
        //      <auth:ValueGreaterThanOrEqual>
        //        (<auth:Value> xs:string </auth:Value> |
        //        <auth:StructuredValue> xs:any </auth:StructuredValue>)
        //      </auth:ValueGreaterThanOrEqual> |
        //      <auth:ValueInRange>
        //        <auth:ValueUpperBound>
        //          (<auth:Value> xs:string </auth:Value> |
        //          <auth:StructuredValue> xs:any </auth:StructuredValue>)
        //        </auth:ValueUpperBound>
        //        <auth:ValueLowerBound>
        //          (<auth:Value> xs:string </auth:Value> |
        //          <auth:StructuredValue> xs:any </auth:StructuredValue>)
        //        </auth:ValueLowerBound>
        //      </auth:ValueInRange> |
        //      <auth:ValueOneOf>
        //        (<auth:Value> xs:string </auth:Value> |
        //         <auth:StructuredValue> xs:any </auth:StructuredValue>) +
        //      </auth:ValueOneOf> ) ?
        //     ...
        //   </auth:ConstrainedValue> ?
        protected virtual ClaimValue ReadClaimValue(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var value = CreateClaimValueInstance();
            ReadCustomAttributes(reader, value);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("Value", AuthNs))
                {
                    value.Value = ReadTrimmedString(reader);
                }
                else if (reader.IsStartElement("StructuredValue", AuthNs))
                {
                    ReadChildrenAsXmlElements(reader, (elt) =>
                    {
                        if (value.StructuredValue == null)
                        {
                            value.StructuredValue = new Collection<XmlElement>();
                        }
                        value.StructuredValue.Add(elt);
                    });
                }
                else
                {
                    return ReadCustomElement(reader, value);
                }
                return true;
            });

            return value;
        }

        protected virtual ConstrainedValue.CompareConstraint ReadCompareConstraint(
            XmlReader reader, ConstrainedValue.CompareConstraint.CompareOperator op)
        {
            var cc = new ConstrainedValue.CompareConstraint(op);
            cc.Value = ReadClaimValue(reader);
            return cc;
        }

        protected virtual ConstrainedValue.RangeConstraint ReadRangeConstraint(XmlReader reader)
        {
            var range = new ConstrainedValue.RangeConstraint();
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("ValueUpperBound", AuthNs))
                {
                    range.UpperBound = ReadClaimValue(reader);
                }
                else if (reader.IsStartElement("ValueLowerBound", AuthNs))
                {
                    range.LowerBound = ReadClaimValue(reader);
                }
                else
                {
                    reader.Skip();
                }
                return true;
            });
            return range;
        }

        protected virtual ConstrainedValue ReadConstrainedValue(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var constraint = CreateConstrainedValueInstance();
            constraint.AssertConstraint = GetBooleanAttribute(reader,
                "AssertConstraint", constraint.AssertConstraint);
            ReadCustomAttributes(reader, constraint);

            ConstrainedValue.ListConstraint listConstraint = null;
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("ValueLessThan", AuthNs))
                {
                    constraint.Constraints.Add(
                        ReadCompareConstraint(reader,
                            ConstrainedValue.CompareConstraint.CompareOperator.Lt));
                }
                else if (reader.IsStartElement("ValueLessThanOrEqual", AuthNs))
                {
                    constraint.Constraints.Add(
                        ReadCompareConstraint(reader,
                            ConstrainedValue.CompareConstraint.CompareOperator.Lte));
                }
                else if (reader.IsStartElement("ValueGreaterThan", AuthNs))
                {
                    constraint.Constraints.Add(
                        ReadCompareConstraint(reader,
                            ConstrainedValue.CompareConstraint.CompareOperator.Gt));
                }
                else if (reader.IsStartElement("ValueGreaterThanOrEqual", AuthNs))
                {
                    constraint.Constraints.Add(
                        ReadCompareConstraint(reader,
                            ConstrainedValue.CompareConstraint.CompareOperator.Gte));
                }
                else if (reader.IsStartElement("ValueInRange", AuthNs))
                {
                    constraint.Constraints.Add(ReadRangeConstraint(reader));
                }
                else if (reader.IsStartElement("ValueOneOf", AuthNs))
                {
                    if (listConstraint == null)
                    {
                        listConstraint = new ConstrainedValue.ListConstraint();
                        constraint.Constraints.Add(listConstraint);
                    }
                    listConstraint.Values.Add(ReadClaimValue(reader));
                }
                else
                {
                    return ReadCustomElement(reader, constraint);
                }
                return true; // hanlded above
            });

            return constraint;
        }

        // <auth:ClaimType Uri="xs:anyURI" Optional="xs:boolean">
        //   <auth:DisplayName  ...> xs:string </auth:DisplayName> ?
        //   <auth:Description  ...> xs:string </auth:Description> ?
        //   <auth:DisplayValue ...> xs:string </auth:DisplayValue> ?
        //   (<auth:Value>...</auth:Value> |
        //    <auth:StructuredValue ...>...</auth:StructuredValue> |
        //    (<auth:EncryptedValue @DecryptionCondition="xs:anyURI">
        //       <xenc:EncryptedData>...</xenc:EncryptedData>
        //     <auth:EncryptedValue>) |
        //    <auth:ConstrainedValue>...</auth:ConstrainedValue>) ?
        //    ...
        // </auth:ClaimType>
        protected virtual DisplayClaim ReadDisplayClaim(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            string uri = reader.GetAttribute("Uri");
            if (string.IsNullOrEmpty(uri))
            {
                throw new MetadataSerializationException("auth:ClaimType is missing a Uri attribute");
            }
            DisplayClaim claim = new DisplayClaim(uri);
            claim.Optional = GetOptionalBooleanAttribute(reader, "Optional");
            ReadCustomAttributes(reader, claim);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("DisplayName", AuthNs))
                {
                    claim.DisplayName = ReadTrimmedString(reader);
                }
                else if (reader.IsStartElement("Description", AuthNs))
                {
                    claim.Description = ReadTrimmedString(reader);
                }
                else if (reader.IsStartElement("DisplayValue", AuthNs))
                {
                    claim.DisplayValue = ReadTrimmedString(reader);
                }
                else if (reader.IsStartElement("Value", AuthNs))
                {
                    claim.Value = ReadTrimmedString(reader);
                }
                else if (reader.IsStartElement("StructuredValue", AuthNs))
                {
                    ReadChildrenAsXmlElements(reader, (elt) =>
                    {
                        if (claim.StructuredValue == null)
                        {
                            claim.StructuredValue = new Collection<XmlElement>();
                            claim.StructuredValue.Add(elt);
                        }
                    });
                }
                else if (reader.IsStartElement("EncryptedValue", AuthNs))
                {
                    claim.EncryptedValue = ReadEncryptedValue(reader);
                }
                else if (reader.IsStartElement("ConstrainedValue", AuthNs))
                {
                    claim.ConstrainedValue = ReadConstrainedValue(reader);
                }
                else
                {
                    return ReadCustomElement(reader, claim);
                }
                return true; // handled above
            });
            return claim;
        }

        void ReadCachedMetadataAttributes(XmlReader reader, ICachedMetadata obj)
        {
            ReadOptionalDateAttribute(reader, "validUntil",
                v => obj.ValidUntil = v);
            ReadOptionalTimespanAttribute(reader, "cacheDuration",
                v => obj.CacheDuration = v);
        }

        // <element name="EntitiesDescriptor" type="md:EntitiesDescriptorType"/>
        // <complexType name="EntitiesDescriptorType">
        //   <sequence>
        //     <element ref="ds:Signature" minOccurs="0"/>
        //     <element ref="md:Extensions" minOccurs="0"/>
        //     <choice minOccurs="1" maxOccurs="unbounded">
        //     <element ref="md:EntityDescriptor"/>
        //     <element ref="md:EntitiesDescriptor"/>
        //     </choice>
        //   </sequence>
        //  <attribute name="validUntil" type="dateTime" use="optional"/>
        //  <attribute name="cacheDuration" type="duration" use="optional"/>
        //  <attribute name="ID" type="ID" use="optional"/>
        //  <attribute name="Name" type="string" use="optional"/>
        // </complexType>
        // <element name="Extensions" type="md:ExtensionsType"/>
        // <complexType final="#all" name="ExtensionsType">
        //   <sequence>
        //     <any namespace="##other" processContents="lax" maxOccurs="unbounded"/>
        //   </sequence>
        // </complexType>
        protected virtual EntitiesDescriptor ReadEntitiesDescriptor(XmlReader reader, SecurityTokenResolver tokenResolver)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var entities = CreateEntitiesDescriptorInstance();

            // This should pick up dsig: elements.  It can also validate the signature,
            // (although I couldn't see where it's grabbing the original token stream
            // from the code)
            var sigReader = new EnvelopedSignatureReader(reader);

            entities.Name = GetAttribute(reader, "Name", entities.Id);
            entities.Id = GetAttribute(reader, "ID", entities.Id);
            ReadCachedMetadataAttributes(reader, entities);
            ReadCustomAttributes(reader, entities);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("EntityDescriptor", Saml2MetadataNs))
                {
                    entities.ChildEntities.Add(ReadEntityDescriptor(reader, tokenResolver));
                }
                else if (reader.IsStartElement("EntitiesDescriptor", Saml2MetadataNs))
                {
                    entities.ChildEntityGroups.Add(ReadEntitiesDescriptor(reader, tokenResolver));
                }
                else if (reader.IsStartElement("Extensions", Saml2MetadataNs))
                {
                    ReadChildrenAsXmlElements(reader, entities.Extensions.Add);
                }
                else
                {
                    return ReadCustomElement(reader, entities);
                }
                return true; // handled above
            });

            // TODO: something with the signature
            return entities;
        }

        public X509RevocationMode RevocationMode { get; set; }
        public List<string> TrustedIssuers { get; } = new List<string>();

        // TODO: call this?
        protected virtual void ValidateSigningCredential(SigningCredentials signingCredentials)
        {
            if (signingCredentials == null)
            {
                throw new ArgumentNullException(nameof(signingCredentials));
            }

            throw new NotImplementedException();

            /*
            if (CertificateValidationMode != X509CertificateValidationMode.Custom)
            {
                CertificateValidator = X509Util.CreateCertificateValidator(CertificateValidationMode, RevocationMode, TrustedStoreLocation);
            }
            else if (CertificateValidationMode == X509CertificateValidationMode.Custom && CertificateValidator == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.GetString(SR.ID4280)));
            }

            X509Certificate2 certificate = GetMetadataSigningCertificate(signingCredentials.SigningKeyIdentifier);

            ValidateIssuer(certificate);
            CertificateValidator.Validate(certificate);*/
        }

        protected virtual void ValidateIssuer(X509Certificate2 signingCertificate)
        {
        }

        protected virtual X509Certificate2 GetMetadataSigningCertificate(SecurityKeyIdentifier ski)
        {
            if (ski == null)
            {
                throw new ArgumentNullException(nameof(ski));
            }
            X509RawDataKeyIdentifierClause clause;
            if (!ski.TryFind<X509RawDataKeyIdentifierClause>(out clause))
            {
                throw new InvalidOperationException("An x509 certificate is not assocaited with the given SecurityKeyIdentifier");
            }
            return new X509Certificate2(clause.GetX509RawData());
        }

        // <element name="Attribute" type="saml:AttributeType"/>
        // <complexType name="AttributeType">
        //   <sequence>
        //     <element ref="saml:AttributeValue" minOccurs="0" maxOccurs="unbounded"/>
        //   </sequence>
        //   <attribute name="Name" type="string" use="required"/>
        //   <attribute name="NameFormat" type="anyURI" use="optional"/>
        //   <attribute name="FriendlyName" type="string" use="optional"/>
        //   <anyAttribute namespace="##other" processContents="lax"/>
        // </complexType>
        protected T ReadSaml2AttributeAttributes<T>(XmlReader reader, Func<string, T> createInstance)
            where T : Saml2Attribute
        {
            string name = GetAttribute(reader, "Name", null);
            if (string.IsNullOrEmpty(name))
            {
                throw new MetadataSerializationException("saml:Attribute without Name attribute");
            }
            var attribute = createInstance(name);
            attribute.NameFormat = GetUriAttribute(reader, "NameFormat", attribute.NameFormat);
            attribute.FriendlyName = GetAttribute(reader, "FriendlyName", attribute.FriendlyName);
            ReadCustomAttributes(reader, attribute);
            return attribute;
        }

        protected bool ReadSaml2AttributeElement(XmlReader reader, Saml2Attribute attribute)
        {
            if (reader.IsStartElement("AttributeValue", Saml2AssertionNs))
            {
                attribute.Values.Add(ReadTrimmedString(reader));
                return true;
            }
            else
            {
                return ReadCustomElement(reader, attribute);
            }
        }

        protected virtual Saml2Attribute ReadSaml2Attribute(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var attribute = ReadSaml2AttributeAttributes(reader, CreateSaml2AttributeInstance);
            ReadCustomAttributes(reader, attribute);
            ReadChildren(reader, () =>
            {
                if (ReadSaml2AttributeElement(reader, attribute))
                {
                }
                else
                {
                    return ReadCustomElement(reader, attribute);
                }
                return true;
            });
            return attribute;
        }

        // <element name="AffiliationDescriptor" type="md:AffiliationDescriptorType"/>
        // <complexType name="AffiliationDescriptorType">
        //   <sequence>
        //     <element ref="ds:Signature" minOccurs="0"/>
        //     <element ref="md:Extensions" minOccurs="0"/>
        //     <element ref="md:AffiliateMember" maxOccurs="unbounded"/>
        //     <element ref="md:KeyDescriptor" minOccurs="0" maxOccurs="unbounded"/>
        //   </sequence>
        //   <attribute name="affiliationOwnerID" type="md:entityIDType"
        //     use="required"/>
        //   <attribute name="validUntil" type="dateTime" use="optional"/>
        //   <attribute name="cacheDuration" type="duration" use="optional"/>
        //   <attribute name="ID" type="ID" use="optional"/>
        //   <anyAttribute namespace="##other" processContents="lax"/>
        // </complexType>
        // <element name="AffiliateMember" type="md:entityIDType"/>
        protected virtual AffiliationDescriptor ReadAffiliationDescriptor(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            // TODO: this can be signed, need to validate the signature
            var descriptor = CreateAffiliationDescriptorInstance();

            string affiliationOwnerID = reader.GetAttribute("affiliationOwnerID");
            if (string.IsNullOrEmpty(affiliationOwnerID))
            {
                throw new MetadataSerializationException("AffiliationDescriptor element without affiliationOwnerID attribute");
            }
            descriptor.AffiliationOwnerId = new EntityId(affiliationOwnerID);
            ReadCachedMetadataAttributes(reader, descriptor);
            descriptor.Id = GetAttribute(reader, "ID", descriptor.Id);
            ReadCustomAttributes(reader, descriptor);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("Extensions", Saml2MetadataNs))
                {
                    ReadChildrenAsXmlElements(reader, descriptor.Extensions.Add);
                }
                else if (reader.IsStartElement("AffiliateMember", Saml2MetadataNs))
                {
                    string member = ReadTrimmedString(reader);
                    if (string.IsNullOrEmpty(member))
                    {
                        throw new MetadataSerializationException("AffiliateMember element without content");
                    }
                    descriptor.AffiliateMembers.Add(new EntityId(member));
                }
                else if (reader.IsStartElement("KeyDescriptor", Saml2MetadataNs))
                {
                    descriptor.KeyDescriptors.Add(ReadKeyDescriptor(reader));
                }
                else
                {
                    return ReadCustomElement(reader, descriptor);
                }
                return true; // handled above
            });

            return descriptor;
        }

        // <element name="AdditionalMetadataLocation"
        //   type="md:AdditionalMetadataLocationType"/>
        // <complexType name="AdditionalMetadataLocationType">
        //   <simpleContent>
        //     <extension base="anyURI">
        //       <attribute name="namespace" type="anyURI" use="required"/>
        //     </extension>
        //   </simpleContent>
        // </complexType>
        protected virtual AdditionalMetadataLocation ReadAdditionalMetadataLocation(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var location = CreateAdditionalMetadataLocationInstance();
            location.Namespace = GetAttribute(reader, "namespace", location.Namespace);
            if (string.IsNullOrEmpty(location.Namespace))
            {
                throw new MetadataSerializationException("AdditionalMetadataLocation element without namespace attribute");
            }
            ReadCustomAttributes(reader, location);
            location.Uri = MakeUri(reader.ReadElementContentAsString());
            return location;
        }

        // <element name="EntityDescriptor" type="md:EntityDescriptorType"/>
        //   <complexType name="EntityDescriptorType">
        //     <sequence>
        //       <element ref="ds:Signature" minOccurs="0"/>
        //       <element ref="md:Extensions" minOccurs="0"/>
        //       <choice>
        //         <choice maxOccurs="unbounded">
        //           <element ref="md:RoleDescriptor"/>
        //           <element ref="md:IDPSSODescriptor"/>
        //           <element ref="md:SPSSODescriptor"/>
        //           <element ref="md:AuthnAuthorityDescriptor"/>
        //           <element ref="md:AttributeAuthorityDescriptor"/>
        //           <element ref="md:PDPDescriptor"/>
        //         </choice>
        //         <element ref="md:AffiliationDescriptor"/>
        //       </choice>
        //       <element ref="md:Organization" minOccurs="0"/>
        //       <element ref="md:ContactPerson" minOccurs="0" maxOccurs="unbounded"/>
        //       <element ref="md:AdditionalMetadataLocation" minOccurs="0"
        //         maxOccurs="unbounded"/>
        //     </sequence>
        //     <attribute name="entityID" type="md:entityIDType" use="required"/>
        //     <attribute name="validUntil" type="dateTime" use="optional"/>
        //     <attribute name="cacheDuration" type="duration" use="optional"/>
        //     <attribute name="ID" type="ID" use="optional"/>
        //     <anyAttribute namespace="##other" processContents="lax"/>
        //   </complexType>
        protected virtual EntityDescriptor ReadEntityDescriptor(XmlReader reader, SecurityTokenResolver tokenResolver)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var descriptor = CreateEntityDescriptorInstance();

            // This should pick up dsig: elements.  It can also validate the signature,
            // (although I couldn't see where it's grabbing the original token stream
            // from the code)
            var sigReader = new EnvelopedSignatureReader(reader);

            string entityId = reader.GetAttribute("entityID");
            if (string.IsNullOrEmpty(entityId))
            {
                throw new MetadataSerializationException("EntityDescriptor is missing required entityID attribute");
            }
            descriptor.EntityId = new EntityId(entityId);
            ReadCachedMetadataAttributes(reader, descriptor);
            descriptor.Id = GetAttribute(reader, "ID", descriptor.Id);

            ReadCustomAttributes(reader, descriptor);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("RoleDescriptor", Saml2MetadataNs))
                {
                    string xsiType = reader.GetAttribute("type", XsiNs);
                    int colonPos = xsiType.IndexOf(':');
                    if (colonPos < 0)
                    {
                        throw new MetadataSerializationException($"Invalid RoleDescriptor extension type {xsiType}");
                    }
                    string extensionNs = reader.LookupNamespace(xsiType.Substring(0, colonPos));
                    string extensionType = xsiType.Substring(colonPos + 1);

                    RoleDescriptor roleDescriptor = null;
                    if (string.Equals(extensionNs, FedNs, StringComparison.Ordinal))
                    {
                        if (string.Equals(extensionType, "ApplicationServiceType", StringComparison.Ordinal))
                        {
                            roleDescriptor = ReadApplicationServiceDescriptor(reader);
                            descriptor.RoleDescriptors.Add(roleDescriptor);
                        }
                        else if (string.Equals(extensionType, "SecurityTokenServiceType", StringComparison.Ordinal))
                        {
                            roleDescriptor = ReadSecurityTokenServiceDescriptor(reader);
                            descriptor.RoleDescriptors.Add(roleDescriptor);
                        }
                    }
                    if (roleDescriptor == null)
                    {
                        ReadCustomRoleDescriptor(xsiType, reader, descriptor);
                    }
                }
                else if (reader.IsStartElement("IDPSSODescriptor", Saml2MetadataNs))
                {
                    descriptor.RoleDescriptors.Add(
                        ReadIdpSsoDescriptor(reader));
                }
                else if (reader.IsStartElement("SPSSODescriptor", Saml2MetadataNs))
                {
                    descriptor.RoleDescriptors.Add(ReadSpSsoDescriptor(reader));
                }
                else if (reader.IsStartElement("AuthnAuthorityDescriptor", Saml2MetadataNs))
                {
                    descriptor.RoleDescriptors.Add(
                        ReadAuthnAuthorityDescriptor(reader));
                }
                else if (reader.IsStartElement("AttributeAuthorityDescriptor", Saml2MetadataNs))
                {
                    descriptor.RoleDescriptors.Add(
                        ReadAttributeAuthorityDescriptor(reader));
                }
                else if (reader.IsStartElement("PDPDescriptor", Saml2MetadataNs))
                {
                    descriptor.RoleDescriptors.Add(
                        ReadPDPDescriptor(reader));
                }
                else if (reader.IsStartElement("AffiliationDescriptor", Saml2MetadataNs))
                {
                    descriptor.AffiliationDescriptors.Add(
                        ReadAffiliationDescriptor(reader));
                }
                else if (reader.IsStartElement("Organization", Saml2MetadataNs))
                {
                    descriptor.Organization = ReadOrganization(reader);
                }
                else if (reader.IsStartElement("ContactPerson", Saml2MetadataNs))
                {
                    descriptor.Contacts.Add(ReadContactPerson(reader));
                }
                else if (reader.IsStartElement("AdditionalMetadataLocation", Saml2MetadataNs))
                {
                    descriptor.AdditionalMetadataLocations.Add(
                        ReadAdditionalMetadataLocation(reader));
                }
                else if (reader.IsStartElement("Extensions", Saml2MetadataNs))
                {
                    ReadChildrenAsXmlElements(reader, descriptor.Extensions.Add);
                }
                else
                {
                    return ReadCustomElement(reader, descriptor);
                }
                return true; // handled above
            });

            // TODO: something with the signature
            return descriptor;
        }

        // <element name="IDPSSODescriptor" type="md:IDPSSODescriptorType"/>
        // <complexType name="IDPSSODescriptorType">
        //   <complexContent>
        //     <extension base="md:SSODescriptorType">
        //     <sequence>
        //       <element ref="md:SingleSignOnService" maxOccurs="unbounded"/>
        //       <element ref="md:NameIDMappingService" minOccurs="0"
        //         maxOccurs="unbounded"/>
        //       <element ref="md:AssertionIDRequestService" minOccurs="0"
        //         maxOccurs="unbounded"/>
        //       <element ref="md:AttributeProfile" minOccurs="0"
        //         maxOccurs="unbounded"/>
        //       <element ref="saml:Attribute" minOccurs="0"
        //         maxOccurs="unbounded"/>
        //       </sequence>
        //       <attribute name="WantAuthnRequestsSigned" type="boolean"
        //         use="optional"/>
        //     </extension>
        //   </complexContent>
        // </complexType>
        // <element name="SingleSignOnService" type="md:EndpointType"/>
        // <element name="NameIDMappingService" type="md:EndpointType"/>
        // <element name="AssertionIDRequestService" type="md:EndpointType"/>
        // <element name="AttributeProfile" type="anyURI"/>
        protected virtual IdpSsoDescriptor ReadIdpSsoDescriptor(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var descriptor = CreateIdpSsoDescriptorInstance();
            descriptor.WantAuthnRequestsSigned = GetOptionalBooleanAttribute(reader, "WantAuthnRequestsSigned");
            ReadSsoDescriptorAttributes(reader, descriptor);
            ReadCustomAttributes(reader, descriptor);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("SingleSignOnService", Saml2MetadataNs))
                {
                    descriptor.SingleSignOnServices.Add(ReadSingleSignOnService(reader));
                }
                else if (reader.IsStartElement("NameIDMappingService", Saml2MetadataNs))
                {
                    descriptor.NameIDMappingServices.Add(ReadNameIDMappingService(reader));
                }
                else if (reader.IsStartElement("AssertionIDRequestService", Saml2MetadataNs))
                {
                    descriptor.AssertionIDRequestServices.Add(ReadAssertionIdRequestService(reader));
                }
                else if (reader.IsStartElement("AttributeProfile", Saml2MetadataNs))
                {
                    descriptor.AttributeProfiles.Add(ReadAttributeProfile(reader));
                }
                else if (reader.IsStartElement("Attribute", Saml2AssertionNs))
                {
                    descriptor.SupportedAttributes.Add(ReadSaml2Attribute(reader));
                }
                else if (ReadSsoDescriptorElement(reader, descriptor))
                {
                }
                else
                {
                    return ReadCustomElement(reader, descriptor);
                }
                return true; // handled above
            });
            return descriptor;
        }

        //  <element name="KeyDescriptor" type="md:KeyDescriptorType"/>
        //  <complexType name="KeyDescriptorType">
        //   <sequence>
        //     <element ref="ds:KeyInfo"/>
        //     <element ref="md:EncryptionMethod" minOccurs="0"
        //      maxOccurs="unbounded"/>
        //   </sequence>
        //   <attribute name="use" type="md:KeyTypes" use="optional"/>
        //  </complexType>
        //  <simpleType name="KeyTypes">
        //   <restriction base="string">
        //     <enumeration value="encryption"/>
        //     <enumeration value="signing"/>
        //   </restriction>
        //  </simpleType>
        //  <element name="EncryptionMethod" type="xenc:EncryptionMethodType"/>
        protected virtual KeyDescriptor ReadKeyDescriptor(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var descriptor = CreateKeyDescriptorInstance();
            string use = reader.GetAttribute("use");
            if (!string.IsNullOrEmpty(use))
            {
                switch (use)
                {
                    case "encryption":
                        descriptor.Use = KeyType.Encryption;
                        break;
                    case "signing":
                        descriptor.Use = KeyType.Signing;
                        break;
                    default:
                        throw new MetadataSerializationException($"Unrecognised key use attribute {use}");
                }
            }
            ReadCustomAttributes(reader, descriptor);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("KeyInfo", DSigNs))
                {
                    descriptor.KeyInfo = ReadDSigKeyInfo(reader);
                    // descriptor.KeyIdentifier = MakeSecurityKeyIdentifier(descriptor.KeyInfo);
                }
                else if (reader.IsStartElement("EncryptionMethod", Saml2MetadataNs))
                {
                    descriptor.EncryptionMethods.Add(ReadEncryptionMethod(reader));
                }
                else
                {
                    return ReadCustomElement(reader, descriptor);
                }
                return true; // handled above
            });

            if (descriptor.KeyInfo == null)
            {
                throw new MetadataSerializationException("Invalid key descriptor with no KeyInfo element");
            }

            return descriptor;
        }

        T ReadLocalizedEntry<T>(XmlReader reader, Func<T> createInstance,
            Action<T, string> setContent) where T : LocalizedEntry
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var entry = createInstance();
            string lang = reader.GetAttribute("lang", XmlNs);
            if (string.IsNullOrEmpty(lang))
            {
                throw new MetadataSerializationException("localized element without xml:lang attribute");
            }
            entry.Language = lang;
            ReadCustomAttributes(reader, entry);

            string value = ReadTrimmedString(reader);
            setContent(entry, value);
            return entry;
        }

        // <complexType name="localizedNameType">
        //   <simpleContent>
        //     <extension base="string">
        //       <attribute ref="xml:lang" use="required"/>
        //     </extension>
        //   </simpleContent>
        // </complexType>
        protected virtual LocalizedName ReadLocalizedName(XmlReader reader) =>
            ReadLocalizedEntry(reader, CreateLocalizedNameInstance, (name, value) => name.Name = value);

        // <complexType name="localizedURIType">
        //   <simpleContent>
        //     <extension base="anyURI">
        //       <attribute ref="xml:lang" use="required"/>
        //     </extension>
        //   </simpleContent>
        // </complexType>
        protected virtual LocalizedUri ReadLocalizedUri(XmlReader reader) =>
            ReadLocalizedEntry(reader, CreateLocalizedUriInstance, (uri, value) => uri.Uri = MakeUri(value));

        // Be loose as sometimes the data is a bit lax (e.g. 'www.foo.com')
        Uri MakeUri(string uri) => new Uri(uri, UriKind.RelativeOrAbsolute);

        public MetadataBase ReadMetadata(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            return ReadMetadata(XmlReader.Create(stream));
        }

        public MetadataBase ReadMetadata(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            return ReadMetadata(reader, NullSecurityTokenResolver.Instance);
        }

        public MetadataBase ReadMetadata(XmlReader reader, SecurityTokenResolver tokenResolver)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            if (tokenResolver == null)
            {
                throw new ArgumentNullException(nameof(tokenResolver));
            }
            return ReadMetadataCore(reader, tokenResolver);
        }

        protected virtual MetadataBase ReadMetadataCore(XmlReader reader, SecurityTokenResolver tokenResolver)
        {
            if (reader.IsStartElement("EntityDescriptor", Saml2MetadataNs))
            {
                return ReadEntityDescriptor(reader, tokenResolver);
            }
            if (reader.IsStartElement("EntitiesDescriptor", Saml2MetadataNs))
            {
                return ReadEntitiesDescriptor(reader, tokenResolver);
            }
            throw new MetadataSerializationException($"Unexpected root entity {reader.Name}");
        }

        // <element name="Organization" type="md:OrganizationType"/>
        // <complexType name="OrganizationType">
        //   <sequence>
        //     <element ref="md:Extensions" minOccurs="0"/>
        //     <element ref="md:OrganizationName" maxOccurs="unbounded"/>
        //     <element ref="md:OrganizationDisplayName" maxOccurs="unbounded"/>
        //     <element ref="md:OrganizationURL" maxOccurs="unbounded"/>
        //   </sequence>
        //   <anyAttribute namespace="##other" processContents="lax"/>
        // </complexType>
        // <element name="OrganizationName" type="md:localizedNameType"/>
        // <element name="OrganizationDisplayName" type="md:localizedNameType"/>
        // <element name="OrganizationURL" type="md:localizedURIType"/>
        protected virtual Organization ReadOrganization(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var org = CreateOrganizationInstance();
            ReadCustomAttributes(reader, org);
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("Extensions", Saml2MetadataNs))
                {
                    ReadChildrenAsXmlElements(reader, org.Extensions.Add);
                }
                else if (reader.IsStartElement("OrganizationName", Saml2MetadataNs))
                {
                    org.Names.Add(ReadLocalizedName(reader));
                }
                else if (reader.IsStartElement("OrganizationDisplayName", Saml2MetadataNs))
                {
                    org.DisplayNames.Add(ReadLocalizedName(reader));
                }
                else if (reader.IsStartElement("OrganizationURL", Saml2MetadataNs))
                {
                    org.Urls.Add(ReadLocalizedUri(reader));
                }
                else
                {
                    return ReadCustomElement(reader, org);
                }
                return true; // handled above
            });
            return org;
        }

        static bool ParseBooleanValue(string v)
        {
            try
            {
                return XmlConvert.ToBoolean(v);
            }
            catch (FormatException e)
            {
                throw new MetadataSerializationException($"Invalid boolean value '{v}'", e);
            }
        }

        static bool? GetOptionalBooleanAttribute(XmlReader reader, string att)
        {
            string sv = reader.GetAttribute(att);
            return string.IsNullOrEmpty(sv) ? (bool?)null : ParseBooleanValue(sv);
        }

        static bool GetBooleanAttribute(XmlReader reader, string att, bool def)
        {
            string sv = reader.GetAttribute(att);
            return string.IsNullOrEmpty(sv) ? def : ParseBooleanValue(sv);
        }

        static void ReadOptionalDateAttribute(XmlReader reader, string attribute, Action<DateTime> dateAction)
        {
            string sv = reader.GetAttribute(attribute);
            if (!string.IsNullOrEmpty(sv))
            {
                DateTime v;
                if (!DateTime.TryParse(sv, out v))
                {
                    throw new MetadataSerializationException(
                        $"Invalid {attribute} attribute value '{sv}'");
                }
                v = v.ToUniversalTime();
                dateAction(v);
            }
        }

        static void ReadOptionalTimespanAttribute(XmlReader reader,
            string attribute, Action<XsdDuration> durationAction)
        {
            string sv = reader.GetAttribute(attribute);
            if (!string.IsNullOrEmpty(sv))
            {
                XsdDuration v;
                if (!XsdDuration.TryParse(sv, out v))
                {
                    throw new MetadataSerializationException(
                        $"Invalid {attribute} attribute value '{sv}'");
                }
                durationAction(v);
            }
        }

        // <element name="RoleDescriptor" type="md:RoleDescriptorType"/>
        //   <complexType name = "RoleDescriptorType" abstract="true">
        //     <sequence>
        //       <element ref="ds:Signature" minOccurs="0"/>
        //       <element ref="md:Extensions" minOccurs="0"/>
        //       <element ref="md:KeyDescriptor" minOccurs="0" maxOccurs="unbounded"/>
        //       <element ref="md:Organization" minOccurs="0"/>
        //       <element ref="md:ContactPerson" minOccurs="0" maxOccurs="unbounded"/>
        //     </sequence>
        //     <attribute name="ID" type="ID" use="optional"/>
        //     <attribute name="validUntil" type="dateTime" use="optional"/>
        //     <attribute name="cacheDuration" type="duration" use="optional"/>
        //     <attribute name="protocolSupportEnumeration" type="md:anyURIListType"
        //       use="required"/>
        //     <attribute name="errorURL" type="anyURI" use="optional"/>
        //     <anyAttribute namespace="##other" processContents="lax"/>
        //   </complexType>
        //   <simpleType name="anyURIListType">
        //     <list itemType="anyURI"/>
        //   </simpleType>
        protected virtual void ReadRoleDescriptorAttributes(XmlReader reader, RoleDescriptor roleDescriptor)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            if (roleDescriptor == null)
            {
                throw new ArgumentNullException(nameof(roleDescriptor));
            }

            // <attribute name="ID" type="ID" use="optional"/>
            roleDescriptor.Id = GetAttribute(reader, "ID", roleDescriptor.Id);

            // <attribute name="validUntil" type="dateTime" use="optional"/>
            // <attribute name="cacheDuration" type="duration" use="optional"/>
            ReadCachedMetadataAttributes(reader, roleDescriptor);

            // <attribute name="protocolSupportEnumeration" type="md:anyURIListType"
            //   use="required"/>
            string protocols = reader.GetAttribute("protocolSupportEnumeration") ?? "";
            foreach (string protocol in protocols.Split(
                new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                roleDescriptor.ProtocolsSupported.Add(MakeUri(protocol));
            }
            if (roleDescriptor.ProtocolsSupported.Count == 0)
            {
                throw new MetadataSerializationException("RoleDescriptor element with no protocolSupportEnumeration attribute");
            }

            // <attribute name="errorURL" type="anyURI" use="optional"/>
            string errorUrl = reader.GetAttribute("errorURL");
            if (!string.IsNullOrEmpty(errorUrl))
            {
                roleDescriptor.ErrorUrl = MakeUri(errorUrl);
            }

            // <anyAttribute namespace="##other" processContents="lax"/>
            ReadCustomAttributes(reader, roleDescriptor);
        }

        protected virtual bool ReadRoleDescriptorElement(XmlReader reader, RoleDescriptor roleDescriptor)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            if (roleDescriptor == null)
            {
                throw new ArgumentNullException(nameof(roleDescriptor));
            }

            // TODO: what about signing??
            // <element ref="ds:Signature" minOccurs="0"/>
            if (reader.IsStartElement("Extensions", Saml2MetadataNs))
            {
                ReadChildrenAsXmlElements(reader, roleDescriptor.Extensions.Add);
            }
            // <element ref="md:ContactPerson" minOccurs="0" maxOccurs="unbounded"/>
            else if (reader.IsStartElement("ContactPerson", Saml2MetadataNs))
            {
                roleDescriptor.Contacts.Add(ReadContactPerson(reader));
            }
            // <element ref="md:KeyDescriptor" minOccurs="0" maxOccurs="unbounded"/>
            else if (reader.IsStartElement("KeyDescriptor", Saml2MetadataNs))
            {
                roleDescriptor.Keys.Add(ReadKeyDescriptor(reader));
            }
            // <element ref="md:Organization" minOccurs="0"/>
            else if (reader.IsStartElement("Organization", Saml2MetadataNs))
            {
                if (roleDescriptor.Organization != null)
                {
                    throw new MetadataSerializationException("RoleDescriptor element with more than one Organization child");
                }
                roleDescriptor.Organization = ReadOrganization(reader);
            }
            // <element ref="md:Extensions" minOccurs="0"/>
            else
            {
                return ReadCustomElement(reader, roleDescriptor);
            }

            return true; // handled above
        }

        // <complexType name="SecurityTokenServiceType">
        //   <extension base="fed:WebServiceDescriptorType">
        //     <sequence>
        //       <element ref="fed:SecurityTokenServiceEndpoint"
        //                minOccurs="1" maxOccurs="unbounded"/>
        //       <element ref="fed:SingleSignOutSubscriptionEndpoint"
        //                minOccurs="0" maxOccurs="unbounded"/>
        //       <element ref="fed:SingleSignOutNotificationEndpoint"
        //                minOccurs="0" maxOccurs="unbounded"/>
        //       <element ref="fed:PassiveRequestorEndpoint"
        //               minOccurs="0" maxOccurs="unbounded"/>
        //      </sequence>
        //   </extension>
        // </complexType>
        // 
        // <element name="fed:SecurityTokenServiceEndpoint"
        //          type="wsa:EndpointReferenceType"/>
        // <element name="fed:SingleSignOutSubscriptionEndpoint"
        //          type="wsa:EndpointReferenceType"/>
        // <element name="fed:SingleSignOutNotificationEndpoint"  
        //          type="wsa:EndpointReferenceType"/>
        // <element name="fed:PassiveRequestorEndpoint"
        //          type="wsa:EndpointReferenceType"/>
        protected virtual SecurityTokenServiceDescriptor ReadSecurityTokenServiceDescriptor(XmlReader reader)
        {
            var descriptor = CreateSecurityTokenServiceDescriptorInstance();
            ReadWebServiceDescriptorAttributes(reader, descriptor);
            ReadCustomAttributes(reader, descriptor);
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("SecurityTokenServiceEndpoint", FedNs))
                {
                    descriptor.SecurityTokenServiceEndpoints.Add(
                        ReadWrappedEndpointReference(reader));
                }
                else if (reader.IsStartElement("SingleSignOutSubscriptionEndpoint", FedNs))
                {
                    descriptor.SingleSignOutSubscriptionEndpoints.Add(
                        ReadWrappedEndpointReference(reader));
                }
                else if (reader.IsStartElement("SingleSignOutNotificationEndpoint", FedNs))
                {
                    descriptor.SingleSignOutNotificationEndpoints.Add(
                        ReadWrappedEndpointReference(reader));
                }
                else if (reader.IsStartElement("PassiveRequestorEndpoint", FedNs))
                {
                    descriptor.PassiveRequestorEndpoints.Add(
                        ReadWrappedEndpointReference(reader));
                }
                else if (ReadWebServiceDescriptorElement(reader, descriptor))
                {
                }
                else
                {
                    return ReadCustomElement(reader, descriptor);
                }
                return true;
            });
            return descriptor;
        }

        // <element name="RequestedAttribute" type="md:RequestedAttributeType"/>
        // <complexType name="RequestedAttributeType">
        // <complexContent>
        //   <extension base="saml:AttributeType">
        //     <attribute name="isRequired" type="boolean" use="optional"/>
        //   </extension>
        // </complexContent>
        // </complexType>
        protected virtual RequestedAttribute ReadRequestedAttribute(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var attribute = ReadSaml2AttributeAttributes(reader, CreateRequestedAttributeInstance);
            attribute.IsRequired = GetOptionalBooleanAttribute(reader, "isRequired");
            ReadChildren(reader, () =>
            {
                if (ReadSaml2AttributeElement(reader, attribute))
                {
                }
                else
                {
                    return ReadCustomElement(reader, attribute);
                }
                return true;
            });
            ReadCustomAttributes(reader, attribute);
            return attribute;
        }

        // <element name="AttributeConsumingService"
        //   type="md:AttributeConsumingServiceType"/>
        // <complexType name="AttributeConsumingServiceType">
        //   <sequence>
        //     <element ref="md:ServiceName" maxOccurs="unbounded"/>
        //     <element ref="md:ServiceDescription" minOccurs="0"
        //       maxOccurs="unbounded"/>
        //     <element ref="md:RequestedAttribute" maxOccurs="unbounded"/>
        //   </sequence>
        //   <attribute name="index" type="unsignedShort" use="required"/>
        //   <attribute name="isDefault" type="boolean" use="optional"/>
        // </complexType>
        // <element name="ServiceName" type="md:localizedNameType"/>
        // <element name="ServiceDescription" type="md:localizedNameType"/>
        protected virtual AttributeConsumingService ReadAttributeConsumingService(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var acs = CreateAttributeConsumingServiceInstance();

            ReadIndexedEntryWithDefaultAttributes(reader, acs);

            ReadCustomAttributes(reader, acs);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("ServiceName", Saml2MetadataNs))
                {
                    acs.ServiceNames.Add(ReadLocalizedName(reader));
                }
                else if (reader.IsStartElement("ServiceDescription", Saml2MetadataNs))
                {
                    acs.ServiceDescriptions.Add(ReadLocalizedName(reader));
                }
                else if (reader.IsStartElement("RequestedAttribute", Saml2MetadataNs))
                {
                    acs.RequestedAttributes.Add(ReadRequestedAttribute(reader));
                }
                else
                {
                    return ReadCustomElement(reader, acs);
                }
                return true; // handled above
            });
            return acs;
        }

        void AddIndexedEntry<T>(IndexedCollectionWithDefault<T> collection, T entry,
            string elName) where T : class, IIndexedEntryWithDefault
        {
            try
            {
                collection.Add(entry.Index, entry);
            }
            catch (ArgumentException e)
            {
                throw new MetadataSerializationException(
                    $"Cannot add the {elName} with index '{entry.Index}' " +
                    "because it repeats an index already used", e);
            }
        }

        //  <element name="SPSSODescriptor" type="md:SPSSODescriptorType"/>
        //  <complexType name="SPSSODescriptorType">
        //    <complexContent>
        //      <extension base="md:SSODescriptorType">
        //        <sequence>
        //        <element ref="md:AssertionConsumerService"
        //          maxOccurs="unbounded"/>
        //        <element ref="md:AttributeConsumingService" minOccurs="0"
        //          maxOccurs="unbounded"/>
        //        </sequence>
        //        <attribute name="AuthnRequestsSigned" type="boolean"
        //          use="optional"/>
        //        <attribute name="WantAssertionsSigned" type="boolean"
        //          use="optional"/>
        //      </extension>
        //    </complexContent>
        //  </complexType>
        //  <element name="AssertionConsumerService" type="md:IndexedEndpoint"/>
        protected virtual SpSsoDescriptor ReadSpSsoDescriptor(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var descriptor = CreateSpSsoDescriptorInstance();
            descriptor.AuthnRequestsSigned = GetBooleanAttribute(reader, "AuthnRequestsSigned", false);
            descriptor.WantAssertionsSigned = GetBooleanAttribute(reader, "WantAssertionsSigned", false);
            ReadSsoDescriptorAttributes(reader, descriptor);
            ReadCustomAttributes(reader, descriptor);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("AssertionConsumerService", Saml2MetadataNs))
                {
                    var acs = ReadAssertionConsumerService(reader);
                    AddIndexedEntry(descriptor.AssertionConsumerServices,
                        acs, "AssertionConsumerService");
                }
                else if (reader.IsStartElement("AttributeConsumingService", Saml2MetadataNs))
                {
                    var acs = ReadAttributeConsumingService(reader);
                    AddIndexedEntry(descriptor.AttributeConsumingServices,
                        acs, "AttributeConsumingService");
                }
                else if (reader.IsStartElement("Extensions", Saml2MetadataNs))
                {
                    var doc = new XmlDocument();
                    XmlNode rootNode = doc.CreateElement("root");
                    doc.AppendChild(rootNode);

                    ReadChildren(reader, () =>
                    {
                        if (reader.IsStartElement("DiscoveryResponse", IdpDiscNs))
                        {
                            var disco = ReadDiscoveryResponse(reader);
                            descriptor.DiscoveryResponses.Add(disco.Index, disco);
                        }
                        else
                        {
                            descriptor.Extensions.Add(ReadCurrentNodeAsXmlElement(reader));
                        }
                        return true;
                    });
                }
                else if (ReadSsoDescriptorElement(reader, descriptor))
                {
                }
                else
                {
                    return ReadCustomElement(reader, descriptor);
                }
                return true; // handled above
            });
            return descriptor;
        }

        // <complexType name="SSODescriptorType" abstract="true">
        //   <complexContent>
        //     <extension base="md:RoleDescriptorType">
        //       <sequence>
        //         <element ref="md:SingleLogoutService" minOccurs="0"
        //           maxOccurs="unbounded"/>
        //         <element ref="md:ManageNameIDService" minOccurs="0"
        //           maxOccurs="unbounded"/>
        //         <element ref="md:NameIDFormat" minOccurs="0"
        //           maxOccurs="unbounded"/>
        //       </sequence>
        //     </extension>
        //   </complexContent>
        // </complexType>
        // <element name="SingleLogoutService" type="md:EndpointType"/>
        // <element name="ManageNameIDService" type="md:EndpointType"/>
        // <element name="NameIDFormat" type="anyURI"/>
        protected virtual void ReadSsoDescriptorAttributes(XmlReader reader, SsoDescriptor descriptor)
        {
            ReadRoleDescriptorAttributes(reader, descriptor);
            ReadCustomAttributes(reader, descriptor);
        }

        protected virtual bool ReadSsoDescriptorElement(XmlReader reader, SsoDescriptor descriptor)
        {
            if (reader.IsStartElement("SingleLogoutService", Saml2MetadataNs))
            {
                descriptor.SingleLogoutServices.Add(
                    ReadSingleLogoutService(reader));
            }
            else if (reader.IsStartElement("ManageNameIDService", Saml2MetadataNs))
            {
                descriptor.ManageNameIDServices.Add(
                    ReadManageNameIDService(reader));
            }
            else if (reader.IsStartElement("NameIDFormat", Saml2MetadataNs))
            {
                descriptor.NameIdentifierFormats.Add(
                    ReadNameIDFormat(reader));
            }
            else
            {
                return ReadRoleDescriptorElement(reader, descriptor);
            }
            return true; // handled above
        }

        Uri GetUriAttribute(XmlReader reader, string att, Uri def)
        {
            string sv = reader.GetAttribute(att);
            return !string.IsNullOrEmpty(sv) ? MakeUri(sv) : def;
        }

        protected virtual void ReadEndpointAttributes(XmlReader reader, Endpoint endpoint)
        {
            endpoint.Binding = GetUriAttribute(reader, "Binding", endpoint.Binding);
            if (endpoint.Binding == null)
            {
                throw new MetadataSerializationException("EndpointType with missing Binding Attribute");
            }

            endpoint.Location = GetUriAttribute(reader, "Location", endpoint.Location);
            if (endpoint.Location == null)
            {
                throw new MetadataSerializationException("EndpointType with missing Location Attribute");
            }

            endpoint.ResponseLocation = GetUriAttribute(reader, "ResponseLocation", endpoint.ResponseLocation);
        }

        static void SkipToElementEnd(XmlReader reader)
        {
            while (reader.IsStartElement())
            {
                reader.Skip();
            }
            reader.ReadEndElement();
        }

        // <complexType name="EndpointType">
        //  <sequence>
        //   <any namespace="##other" processContents="lax" minOccurs="0"
        //     maxOccurs="unbounded"/>
        //  </sequence>
        //  <attribute name="Binding" type="anyURI" use="required"/>
        //  <attribute name="Location" type="anyURI" use="required"/>
        //  <attribute name="ResponseLocation" type="anyURI" use="optional"/>
        //  <anyAttribute namespace="##other" processContents="lax"/>
        // </complexType>
        T ReadWrappedEndpoint<T>(XmlReader reader, Func<T> createInstance) where T : Endpoint
        {
            var endpoint = createInstance();
            ReadEndpointAttributes(reader, endpoint);
            ReadCustomAttributes(reader, endpoint);
            ReadChildren(reader, () => false);
            return endpoint;
        }

        protected virtual Endpoint ReadEndpoint(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreateEndpointInstance);

        protected virtual AttributeService ReadAttributeService(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreateAttributeServiceInstance);

        protected virtual AuthnQueryService ReadAuthnQueryService(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreateAuthnQueryServiceInstance);

        protected virtual AssertionIdRequestService ReadAssertionIdRequestService(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreateAssertionIdRequestServiceInstance);

        protected virtual AuthzService ReadAuthzService(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreateAuthzServiceInstance);

        protected virtual SingleLogoutService ReadSingleLogoutService(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreateSingleLogoutServiceInstance);

        protected virtual ManageNameIDService ReadManageNameIDService(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreateManageNameIDServiceInstance);

        protected virtual SingleSignOutNotificationEndpoint ReadSingleSignOutNotificationEndpoint(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreateSingleSignOutNotificationEndpointInstance);

        protected virtual ApplicationServiceEndpoint ReadApplicationServiceEndpoint(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreateApplicationServiceEndpointInstance);

        protected virtual PassiveRequestorEndpoint ReadPassiveRequestorEndpoint(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreatePassiveRequestorEndpointInstance);

        protected virtual SingleSignOnService ReadSingleSignOnService(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreateSingleSignOnServiceInstance);

        protected virtual NameIDMappingService ReadNameIDMappingService(XmlReader reader) =>
            ReadWrappedEndpoint(reader, CreateNameIDMappingServiceInstance);

        void ReadIndexedEntryWithDefaultAttributes(XmlReader reader, IIndexedEntryWithDefault entry)
        {
            string sv = reader.GetAttribute("index");
            if (string.IsNullOrEmpty(sv))
            {
                throw new MetadataSerializationException("IndexedEndpoint element missing index attribute");
            }

            int index;
            if (!int.TryParse(sv, out index))
            {
                throw new MetadataSerializationException($"IndexedEndpoint element with invalid index attribute '{index}'");
            }
            entry.Index = index;
            entry.IsDefault = GetOptionalBooleanAttribute(reader, "isDefault");
        }

        void ReadIndexedEndpointAttributes(XmlReader reader, IndexedEndpoint endpoint)
        {
            ReadIndexedEntryWithDefaultAttributes(reader, endpoint);
            ReadEndpointAttributes(reader, endpoint);
        }

        // <complexType name="IndexedEndpoint">
        //   <complexContent>
        //     <extension base="md:EndpointType">
        //       <attribute name="index" type="unsignedShort" use="required"/>
        //       <attribute name="isDefault" type="boolean" use="optional"/>
        //     </extension>
        //   </complexContent>
        // </complexType>
        T ReadWrappedIndexedEndpoint<T>(XmlReader reader, Func<T> createInstance) where T : IndexedEndpoint
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var endpoint = createInstance();
            ReadIndexedEndpointAttributes(reader, endpoint);
            ReadCustomAttributes(reader, endpoint);
            ReadChildren(reader, () => false);
            return endpoint;
        }

        protected virtual AssertionConsumerService ReadAssertionConsumerService(XmlReader reader) =>
            ReadWrappedIndexedEndpoint(reader, CreateAssertionConsumerServiceInstance);

        protected virtual DiscoveryResponse ReadDiscoveryResponse(XmlReader reader) =>
            ReadWrappedIndexedEndpoint(reader, CreateDiscoveryResponseInstance);

        // <element name="PDPDescriptor" type="md:PDPDescriptorType"/>
        // <complexType name="PDPDescriptorType">
        //   <complexContent>
        //     <extension base="md:RoleDescriptorType">
        //       <sequence>
        //         <element ref="md:AuthzService" maxOccurs="unbounded"/>
        //         <element ref="md:AssertionIDRequestService" minOccurs="0"
        //           maxOccurs="unbounded"/>
        //         <element ref="md:NameIDFormat" minOccurs="0"
        //           maxOccurs="unbounded"/>
        //       </sequence>
        //     </extension>
        //   </complexContent>
        // </complexType>
        // <element name="AuthzService" type="md:EndpointType"/>
        protected virtual PDPDescriptor ReadPDPDescriptor(XmlReader reader)
        {
            var descriptor = CreatePDPDescriptorInstance();
            ReadRoleDescriptorAttributes(reader, descriptor);
            ReadCustomAttributes(reader, descriptor);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("AuthzService", Saml2MetadataNs))
                {
                    descriptor.AuthzServices.Add(ReadAuthzService(reader));
                }
                else if (reader.IsStartElement("AssertionIDRequestService", Saml2MetadataNs))
                {
                    descriptor.AssertionIdRequestServices.Add(
                        ReadAssertionIdRequestService(reader));
                }
                else if (reader.IsStartElement("NameIDFormat", Saml2MetadataNs))
                {
                    descriptor.NameIDFormats.Add(ReadNameIDFormat(reader));
                }
                else if (ReadRoleDescriptorElement(reader, descriptor))
                {
                }
                else
                {
                    return ReadCustomElement(reader, descriptor);
                }
                return true; // handled above
            });
            return descriptor;
        }

        protected virtual NameIDFormat ReadNameIDFormat(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var nameIDFormat = CreateNameIDFormatInstance();
            string uri = ReadTrimmedString(reader);
            if (string.IsNullOrEmpty(uri))
            {
                throw new MetadataSerializationException("NameIDFormat element with no uri");
            }
            nameIDFormat.Uri = MakeUri(uri);
            return nameIDFormat;
        }


        // <element name="AuthnAuthorityDescriptor"
        //   type="md:AuthnAuthorityDescriptorType"/>
        //   <complexType name="AuthnAuthorityDescriptorType">
        //     <complexContent>
        //       <extension base="md:RoleDescriptorType">
        //         <sequence>
        //           <element ref="md:AuthnQueryService" maxOccurs="unbounded"/>
        //           <element ref="md:AssertionIDRequestService" minOccurs="0"
        //             maxOccurs="unbounded"/>
        //           <element ref="md:NameIDFormat" minOccurs="0"
        //             maxOccurs="unbounded"/>
        //         </sequence>
        //       </extension>
        //     </complexContent>
        //   </complexType>
        // <element name="AuthnQueryService" type="md:EndpointType"/>
        protected virtual AuthnAuthorityDescriptor ReadAuthnAuthorityDescriptor(XmlReader reader)
        {
            var descriptor = CreateAuthnAuthorityDescriptorInstance();
            ReadRoleDescriptorAttributes(reader, descriptor);
            ReadCustomAttributes(reader, descriptor);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("AuthnQueryService", Saml2MetadataNs))
                {
                    descriptor.AuthnQueryServices.Add(ReadAuthnQueryService(reader));
                }
                else if (reader.IsStartElement("AssertionIDRequestService", Saml2MetadataNs))
                {
                    descriptor.AssertionIdRequestServices.Add(ReadAssertionIdRequestService(reader));
                }
                else if (reader.IsStartElement("NameIDFormat", Saml2MetadataNs))
                {
                    descriptor.NameIDFormats.Add(ReadNameIDFormat(reader));
                }
                else if (ReadRoleDescriptorElement(reader, descriptor))
                {
                }
                else
                {
                    return ReadCustomElement(reader, descriptor);
                }
                return true; // handled above
            });
            return descriptor;
        }

        protected virtual AttributeProfile ReadAttributeProfile(XmlReader reader)
        {
            var profile = CreateAttributeProfileInstance();
            ReadCustomAttributes(reader, profile);
            profile.Uri = MakeUri(reader.ReadElementContentAsString());
            return profile;
        }

        // <element name="AttributeAuthorityDescriptor"
        //   type="md:AttributeAuthorityDescriptorType"/>
        //   <complexType name="AttributeAuthorityDescriptorType">
        //     <complexContent>
        //       <extension base="md:RoleDescriptorType">
        //       <sequence>
        //         <element ref="md:AttributeService" maxOccurs="unbounded"/>
        //         <element ref="md:AssertionIDRequestService" minOccurs="0"
        //           maxOccurs="unbounded"/>
        //         <element ref="md:NameIDFormat" minOccurs="0"
        //           maxOccurs="unbounded"/>
        //         <element ref="md:AttributeProfile" minOccurs="0"
        //           maxOccurs="unbounded"/>
        //         <element ref="saml:Attribute" minOccurs="0"
        //           maxOccurs="unbounded"/>
        //         </sequence>
        //       </extension>
        //     </complexContent>
        //   </complexType>
        // <element name="AttributeService" type="md:EndpointType"/>
        protected virtual AttributeAuthorityDescriptor ReadAttributeAuthorityDescriptor(XmlReader reader)
        {
            var descriptor = CreateAttributeAuthorityDescriptorInstance();
            ReadRoleDescriptorAttributes(reader, descriptor);
            ReadCustomAttributes(reader, descriptor);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("AttributeService", Saml2MetadataNs))
                {
                    descriptor.AttributeServices.Add(ReadAttributeService(reader));
                }
                else if (reader.IsStartElement("AssertionIDRequestService", Saml2MetadataNs))
                {
                    descriptor.AssertionIdRequestServices.Add(ReadAssertionIdRequestService(reader));
                }
                else if (reader.IsStartElement("NameIDFormat", Saml2MetadataNs))
                {
                    descriptor.NameIDFormats.Add(ReadNameIDFormat(reader));
                }
                else if (reader.IsStartElement("AttributeProfile", Saml2MetadataNs))
                {
                    descriptor.AttributeProfiles.Add(ReadAttributeProfile(reader));
                }
                else if (reader.IsStartElement("Attribute", Saml2AssertionNs))
                {
                    descriptor.Attributes.Add(ReadSaml2Attribute(reader));
                }
                else if (ReadRoleDescriptorElement(reader, descriptor))
                {
                }
                else
                {
                    return ReadCustomElement(reader, descriptor);
                }
                return true; // handled above
            });

            return descriptor;
        }

        // <complexType name="WebServiceDescriptorType" abstract="true">
        // 	<complexContent>
        //	  <extension base="md:RoleDescriptorType">
        // 	    <sequence>
        // 	      <element ref="fed:LogicalServiceNamesOffered" minOccurs="0" maxOccurs="1" />
        //        <element ref="fed:TokenTypesOffered" minOccurs="0" maxOccurs="1" />
        // 	      <element ref="fed:ClaimDialectsOffered" minOccurs="0" maxOccurs="1" />
        // 	      <element ref="fed:ClaimTypesOffered" minOccurs="0" maxOccurs="1" />
        // 	      <element ref="fed:ClaimTypesRequested" minOccurs="0" maxOccurs="1"/>
        // 	      <element ref="fed:AutomaticPseudonyms" minOccurs="0" maxOccurs="1"/>
        // 	      <element ref="fed:TargetScopes" minOccurs="0" maxOccurs="1"/>
        // 	    </sequence>
        // 	    <attribute name="ServiceDisplayName" type="xs:String" use="optional"/>
        // 	    <attribute name="ServiceDescription" type="xs:String" use="optional"/>
        // 	  </extension>
        // 	</complexContent>
        // </complexType>
        // <element name='LogicalServiceNamesOffered' type=fed:LogicalServiceNamesOfferedType' />
        // <element name="fed:TokenTypeOffered" type="fed:TokenType"/>
        // <element name="fed:ClaimDialectsOffered" type="fed:ClaimDialectsOfferedType"/>
        // <element name="fed:ClaimTypesOffered" type="fed:ClaimTypesOfferedType"/>
        // <element name="ClaimTypesRequested" type="tns:ClaimTypesRequestedType"/>
        // <element name="fed:AutomaticPseudonyms" type="xs:boolean"/>
        // <element name="fed:TargetScope" type="tns:EndpointType"/>
        protected virtual void ReadWebServiceDescriptorAttributes(XmlReader reader, WebServiceDescriptor descriptor)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            ReadRoleDescriptorAttributes(reader, descriptor);
            descriptor.ServiceDisplayName = GetAttribute(reader,
                "ServiceDisplayName", descriptor.ServiceDisplayName);
            descriptor.ServiceDescription = GetAttribute(reader,
                "ServiceDescription", descriptor.ServiceDescription);
            ReadCustomAttributes(reader, descriptor);
        }

        void ReadUris(XmlReader reader, string parentElementName,
            string childElementName, ICollection<Uri> uris)
        {
            bool empty = reader.IsEmptyElement;
            reader.ReadStartElement();
            if (!empty)
            {
                while (reader.IsStartElement())
                {
                    if (!reader.IsStartElement(childElementName, FedNs))
                    {
                        throw new MetadataSerializationException(
                            $"<fed:{parentElementName}> element with unexpected child {reader.Name}");
                    }
                    string uri = reader.GetAttribute("Uri");
                    if (string.IsNullOrEmpty(uri))
                    {
                        throw new MetadataSerializationException(
                            $"<fed:{childElementName}> element without Uri attribute");
                    }
                    uris.Add(MakeUri(uri));
                    reader.Skip();
                }
                reader.ReadEndElement();
            }
        }

        static void ReadChildren(XmlReader reader, Func<bool> childAction)
        {
            bool empty = reader.IsEmptyElement;
            reader.ReadStartElement();
            if (!empty)
            {
                while (reader.NodeType != XmlNodeType.EndElement)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (!childAction())
                        {
                            reader.Skip();
                        }
                    }
                    else
                    {
                        reader.Read();
                    }
                }
                reader.ReadEndElement();
            }
        }

        void ReadDisplayClaims(XmlReader reader, ICollection<DisplayClaim> claims)
        {
            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("ClaimType", AuthNs))
                {
                    claims.Add(ReadDisplayClaim(reader));
                    return true;
                }
                return false;
            });
        }

        XmlElement ReadCurrentNodeAsXmlElement(XmlReader reader)
        {
            bool empty = reader.IsEmptyElement;
            var doc = new XmlDocument();
            using (var subtreeReader = reader.ReadSubtree())
            {
                doc.Load(subtreeReader);
            }
            if (!empty)
            {
                reader.ReadEndElement();
            }
            else
            {
                reader.Skip();
            }
            return doc.DocumentElement;
        }

        void ReadChildrenAsXmlElements(XmlReader reader, Action<XmlElement> childAction)
        {
            ReadChildren(reader, () =>
            {
                childAction(ReadCurrentNodeAsXmlElement(reader));
                return true;
            });
        }

        string ReadTrimmedString(XmlReader reader) =>
            (reader.ReadElementContentAsString() ?? "").Trim();

        //  <xs:complexType name="ServiceNameType">
        //    <xs:simpleContent>
        //      <xs:extension base="xs:QName">
        //        <xs:attribute name="PortName" type="xs:NCName"/>
        //        <xs:anyAttribute namespace="##other" processContents="lax"/>
        //      </xs:extension>
        //    </xs:simpleContent>
        //  </xs:complexType>
        protected virtual ServiceName ReadServiceName(XmlReader reader)
        {
            var serviceName = CreateServiceNameInstance();
            serviceName.PortName = GetAttribute(reader, "PortName", serviceName.PortName);
            ReadCustomAttributes(reader, serviceName);
            serviceName.Name = ReadTrimmedString(reader);
            return serviceName;
        }

        //  <xs:element name="EndpointReference" type="wsa:EndpointReferenceType"/>
        //  <xs:complexType name="EndpointReferenceType">
        //    <xs:sequence>
        //      <xs:element name="Address" type="wsa:AttributedURI"/>
        //      <xs:element name="ReferenceProperties" type="wsa:ReferencePropertiesType" minOccurs="0"/>
        //      <xs:element name="ReferenceParameters" type="wsa:ReferenceParametersType" minOccurs="0"/>
        //      <xs:element name="PortType" type="wsa:AttributedQName" minOccurs="0"/>
        //      <xs:element name="ServiceName" type="wsa:ServiceNameType" minOccurs="0"/>
        //      <xs:any namespace="##other" processContents="lax" minOccurs="0" maxOccurs="unbounded">
        //        <xs:annotation>
        //          <xs:documentation>
        // 				 If "Policy" elements from namespace "http://schemas.xmlsoap.org/ws/2002/12/policy#policy" are used, they must appear first (before any extensibility elements).
        // 				</xs:documentation>
        //        </xs:annotation>
        //      </xs:any>
        //    </xs:sequence>
        //    <xs:anyAttribute namespace="##other" processContents="lax"/>
        //  </xs:complexType>
        //  <xs:complexType name="ReferencePropertiesType">
        //    <xs:sequence>
        //      <xs:any processContents="lax" minOccurs="0" maxOccurs="unbounded"/>
        //    </xs:sequence>
        //  </xs:complexType>
        //  <xs:complexType name="ReferenceParametersType">
        //    <xs:sequence>
        //      <xs:any processContents="lax" minOccurs="0" maxOccurs="unbounded"/>
        //    </xs:sequence>
        //  </xs:complexType>
        protected virtual EndpointReference ReadEndpointReference(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var endpointReference = CreateEndpointReferenceInstance();
            ReadCustomAttributes(reader, endpointReference);

            ReadChildren(reader, () =>
            {
                if (reader.IsStartElement("Address", WsaNs))
                {
                    if (endpointReference.Uri != null)
                    {
                        throw new MetadataSerializationException("<wsa:EndpointReference> has more than one <wsa:Address> child");
                    }
                    endpointReference.Uri = MakeUri(ReadTrimmedString(reader));
                }
                else if (reader.IsStartElement("ReferenceProperties", WsaNs))
                {
                    ReadChildrenAsXmlElements(reader, endpointReference.ReferenceProperties.Add);
                }
                else if (reader.IsStartElement("ReferenceParameters", WsaNs))
                {
                    ReadChildrenAsXmlElements(reader, endpointReference.ReferenceParameters.Add);
                }
                else if (reader.IsStartElement("Metadata", WsaNs))
                {
                    ReadChildrenAsXmlElements(reader, endpointReference.Metadata.Add);
                }
                else if (reader.IsStartElement("PortType", WsaNs))
                {
                    endpointReference.PortType = ReadTrimmedString(reader);
                }
                else if (reader.IsStartElement("ServiceName", WsaNs))
                {
                    endpointReference.ServiceName = ReadServiceName(reader);
                }
                else if (reader.IsStartElement("Policy", WspNs))
                {
                    ReadChildrenAsXmlElements(reader, endpointReference.Policies.Add);
                }
                else
                {
                    return ReadCustomElement(reader, endpointReference);
                }
                return true; // handled above
            });
            return endpointReference;
        }

        protected virtual EndpointReference ReadWrappedEndpointReference(XmlReader reader)
        {
            (string wrapperName, string wrapperNs) = (reader.Name, reader.NamespaceURI);
            while (reader.Read() && reader.NodeType != XmlNodeType.Element)
            {
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    throw new MetadataSerializationException(
                        "Expected a wsa:EndpointReference node but encountered an empty element");
                }
            }
            if (!reader.IsStartElement("EndpointReference", WsaNs))
            {
                throw new MetadataSerializationException(
                    $"Expected a wsa:EndpointReference node but encountered {reader.Name}");
            }
            var result = ReadEndpointReference(reader);
            while (reader.NodeType != XmlNodeType.EndElement && reader.Read())
            {
            }
            if (!reader.Name.Equals(wrapperName, StringComparison.Ordinal) ||
                !reader.NamespaceURI.Equals(wrapperNs))
            {
                throw new MetadataSerializationException(
                    $"Expected end of element '{wrapperName}' in namespace '{wrapperNs}' " +
                    $"but encountered '{reader.Name}' in namespace '{reader.NamespaceURI}'");
            }
            reader.Read();
            return result;
        }

        protected virtual bool ReadWebServiceDescriptorElement(XmlReader reader, WebServiceDescriptor descriptor)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (ReadRoleDescriptorElement(reader, descriptor))
            {
                return true;
            }
            // <element ref="fed:LogicalServiceNamesOffered" minOccurs="0" maxOccurs="1" />
            if (reader.IsStartElement("LogicalServiceNamesOffered", FedNs))
            {
                // <fed:LogicalServiceNamesOffered ...>
                //	<fed:IssuerName Uri="xs:anyURI" .../> +
                // </fed:LogicalServiceNamesOffered>
                ReadUris(reader, "LogicalServiceNamesOffered",
                    "IssuerName", descriptor.LogicalServiceNamesOffered);
            }
            // <element ref="fed:TokenTypesOffered" minOccurs="0" maxOccurs="1" />
            else if (reader.IsStartElement("TokenTypesOffered", FedNs))
            {
                // <fed:TokenTypesOffered ...>
                //   <fed:TokenType Uri="xs:anyURI" ...>
                //	   ...
                //   </fed:TokenType> + 
                //     ...
                // </fed:TokenTypesOffered>
                ReadUris(reader, "TokenTypesOffered",
                    "TokenType", descriptor.TokenTypesOffered);
            }
            // <element ref="fed:ClaimDialectsOffered" minOccurs="0" maxOccurs="1" />
            else if (reader.IsStartElement("ClaimDialectsOffered", FedNs))
            {
                // <fed:ClaimDialectsOffered>
                //   <fed:ClaimDialect Uri="xs:anyURI" /> +
                // </fed:ClaimDialectsOffered>			
                ReadUris(reader, "ClaimDialectsOffered",
                    "ClaimDialect", descriptor.ClaimDialectsOffered);
            }
            // <element ref="fed:ClaimTypesOffered" minOccurs="0" maxOccurs="1" />
            else if (reader.IsStartElement("ClaimTypesOffered", FedNs))
            {
                // <fed:ClaimTypesOffered ...>
                //   <auth:ClaimType ...> ... </auth:ClaimType> +
                // </fed:ClaimTypesOffered>
                ReadDisplayClaims(reader, descriptor.ClaimTypesOffered);
            }
            // <element ref="fed:ClaimTypesRequested" minOccurs="0" maxOccurs="1"/>
            else if (reader.IsStartElement("ClaimTypesRequested", FedNs))
            {
                // <fed:ClaimTypesRequested ...>
                //   <auth:ClaimType ...> ... </auth:ClaimType> +
                // </fed:ClaimTypesRequested>
                ReadDisplayClaims(reader, descriptor.ClaimTypesRequested);
            }
            // <element ref="fed:AutomaticPseudonyms" minOccurs="0" maxOccurs="1"/>
            else if (reader.IsStartElement("AutomaticPseudonyms", FedNs))
            {
                // <fed:AutomaticPseudonyms>
                //   xs:boolean
                // </fed:AutomaticPseudonyms>
                descriptor.AutomaticPseudonyms = reader.ReadElementContentAsBoolean();
            }
            // <element ref="fed:TargetScopes" minOccurs="0" maxOccurs="1"/>
            else if (reader.IsStartElement("TargetScopes", FedNs))
            {
                // <fed:TargetScopes ...>
                //   <wsa:EndpointReference>
                //     ...
                //   </wsa:EndpointReference> + 
                // </fed:TargetScopes>
                ReadChildren(reader, () =>
                {
                    if (reader.IsStartElement("EndpointReference", WsaNs))
                    {
                        descriptor.TargetScopes.Add(ReadEndpointReference(reader));
                        return true;
                    }
                    return false;
                });
            }
            else
            {
                return ReadCustomElement(reader, descriptor);
            }
            return true; // handled above
        }

        static void WriteWrappedElements(XmlWriter writer, string wrapPrefix,
            string wrapName, string wrapNs, IEnumerable<XmlElement> elts)
        {
            if (elts.Any())
            {
                writer.WriteStartElement(wrapPrefix, wrapName, wrapNs);
                foreach (var elt in elts)
                {
                    elt.WriteTo(writer);
                }
                writer.WriteEndElement();
            }
        }

        protected virtual void WriteServiceName(XmlWriter writer, ServiceName serviceName)
        {
            writer.WriteStartElement("wsa", "ServiceName", WsaNs);
            WriteCustomAttributes(writer, serviceName);
            if (!string.IsNullOrEmpty(serviceName.PortName))
            {
                writer.WriteAttributeString("PortName", serviceName.PortName);
            }
            if (!string.IsNullOrEmpty(serviceName.Name))
            {
                writer.WriteString(serviceName.Name);
            }
            writer.WriteEndElement();
        }

        protected virtual void WriteEndpointReference(XmlWriter writer, EndpointReference endpointReference)
        {
            writer.WriteStartElement("wsa", "EndpointReference", WsaNs);
            WriteCustomAttributes(writer, endpointReference);
            writer.WriteStartElement("wsa", "Address", WsaNs);
            writer.WriteString(endpointReference.Uri.ToString());
            writer.WriteEndElement();

            WriteWrappedElements(writer, "wsa", "ReferenceProperties", WsaNs,
                endpointReference.ReferenceProperties);
            WriteWrappedElements(writer, "wsa", "ReferenceParameters", WsaNs,
                endpointReference.ReferenceParameters);
            if (!string.IsNullOrEmpty(endpointReference.PortType))
            {
                writer.WriteStartElement("wsa", "PortType", WsaNs);
                writer.WriteString(endpointReference.PortType);
                writer.WriteEndElement();
            }
            if (endpointReference.ServiceName != null)
            {
                WriteServiceName(writer, endpointReference.ServiceName);
            }
            WriteWrappedElements(writer, "wsa", "Metadata", WsaNs,
                endpointReference.Metadata);
            if (endpointReference.Policies.Count > 0)
            {
                foreach (var polElt in endpointReference.Policies)
                {
                    writer.WriteStartElement("wsp", "Policy", WspNs);
                    polElt.WriteTo(writer);
                    writer.WriteEndElement();
                }
            }
            WriteWrappedElements(writer, "wsp", "Policy", WspNs,
                endpointReference.Policies);
            WriteCustomElements(writer, endpointReference);

            writer.WriteEndElement();
        }

        void WriteEndpointReferences(XmlWriter writer, string elName, string elNs,
            ICollection<EndpointReference> endpointReferences)
        {
            foreach (var endpointReference in endpointReferences)
            {
                writer.WriteStartElement(elName, elNs);
                WriteEndpointReference(writer, endpointReference);
                writer.WriteEndElement();
            }
        }

        protected virtual void WriteApplicationServiceDescriptor(XmlWriter writer, ApplicationServiceDescriptor appService)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (appService == null)
            {
                throw new ArgumentNullException(nameof(appService));
            }
            writer.WriteStartElement("RoleDescriptor", Saml2MetadataNs);
            writer.WriteAttributeString("xsi", "type", XsiNs, "fed:ApplicationServiceType");
            writer.WriteAttributeString("xmlns", "fed", null, FedNs);

            WriteWebServiceDescriptorAttributes(writer, appService);
            WriteCustomAttributes(writer, appService);

            WriteWebServiceDescriptorElements(writer, appService);

            WriteEndpointReferences(writer, "ApplicationServiceEndpoint",
                FedNs, appService.Endpoints);
            WriteEndpointReferences(writer, "SingleSignOutNotificationEndpoint",
                FedNs, appService.SingleSignOutEndpoints);
            WriteEndpointReferences(writer, "PassiveRequestorEndpoint",
                FedNs, appService.PassiveRequestorEndpoints);
            writer.WriteEndElement();
        }

        static void WriteStringElementIfPresent(XmlWriter writer, string elName,
            string elNs, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                writer.WriteElementString(elName, elNs, value);
            }
        }

        static void WriteBase64Element(XmlWriter writer, string elName,
            string elNs, byte[] value)
        {
            if (value != null)
            {
                writer.WriteElementString(elName, elNs, Convert.ToBase64String(value));
            }
        }

        static void WriteStringAttributeIfPresent(XmlWriter writer, string attName,
            string attNs, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                writer.WriteAttributeString(attName, attNs, value);
            }
        }

        static void WriteUriAttributeIfPresent(XmlWriter writer, string attName,
            string attNs, Uri value)
        {
            if (value != null)
            {
                writer.WriteAttributeString(attName, attNs, value.ToString());
            }
        }

        static void WriteBooleanAttribute(XmlWriter writer, string attName,
            string attNs, bool? value)
        {
            if (value.HasValue)
            {
                writer.WriteAttributeString(attName, attNs, value.Value ? "true" : "false");
            }
        }

        static void WriteStringElements(XmlWriter writer, string elName, string elNs,
            IEnumerable<string> values)
        {
            foreach (string value in values)
            {
                writer.WriteElementString(elName, elNs, value);
            }
        }

        protected virtual void WriteContactPerson(XmlWriter writer, ContactPerson contactPerson)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (contactPerson == null)
            {
                throw new ArgumentNullException(nameof(contactPerson));
            }

            writer.WriteStartElement("ContactPerson", Saml2MetadataNs);
            writer.WriteAttributeString("contactType", ContactTypeHelpers.ToString(contactPerson.Type));
            WriteCustomAttributes(writer, contactPerson);
            WriteWrappedElements(writer, null, "Extensions", Saml2MetadataNs,
                contactPerson.Extensions);
            WriteStringElementIfPresent(writer, "Company", Saml2MetadataNs, contactPerson.Company);
            WriteStringElementIfPresent(writer, "GivenName", Saml2MetadataNs, contactPerson.GivenName);
            WriteStringElementIfPresent(writer, "SurName", Saml2MetadataNs, contactPerson.Surname);
            WriteStringElements(writer, "EmailAddress", Saml2MetadataNs, contactPerson.EmailAddresses);
            WriteStringElements(writer, "TelephoneNumber", Saml2MetadataNs, contactPerson.TelephoneNumbers);
            WriteCustomElements(writer, contactPerson);

            writer.WriteEndElement();
        }

        protected virtual void WriteCustomAttributes<T>(XmlWriter writer, T source)
        {
        }

        protected virtual void WriteCustomElements<T>(XmlWriter writer, T source)
        {
        }

        protected virtual void WriteEndpointAttributes(XmlWriter writer, Endpoint endpoint)
        {
            writer.WriteAttributeString("Binding", endpoint.Binding.ToString());
            writer.WriteAttributeString("Location", endpoint.Location.ToString());
            WriteUriAttributeIfPresent(writer, "ResponseLocation", null, endpoint.ResponseLocation);
            WriteCustomAttributes(writer, endpoint);
        }

        protected virtual void WriteEndpoint(XmlWriter writer, Endpoint endpoint,
            string name, string ns)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (ns == null)
            {
                throw new ArgumentNullException(nameof(ns));
            }
            writer.WriteStartElement(name, ns);
            WriteEndpointAttributes(writer, endpoint);
            WriteCustomAttributes(writer, endpoint);
            WriteCustomElements(writer, endpoint);
            writer.WriteEndElement();
        }

        protected virtual void WriteIndexedEndpoint(XmlWriter writer, IndexedEndpoint endpoint,
            string name, string ns)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (ns == null)
            {
                throw new ArgumentNullException(nameof(ns));
            }
            writer.WriteStartElement(name, ns);
            WriteEndpointAttributes(writer, endpoint);
            WriteBooleanAttribute(writer, "isDefault", null, endpoint.IsDefault);
            writer.WriteAttributeString("index", endpoint.Index.ToString());
            WriteCustomAttributes(writer, endpoint);
            WriteCustomElements(writer, endpoint);
            writer.WriteEndElement();
        }

        protected virtual void WriteEndpoints(XmlWriter writer,
            IEnumerable<Endpoint> endpoints, string name, string ns) =>
                WriteCollection(writer, endpoints, (writer_, endpoint) =>
                    WriteEndpoint(writer_, endpoint, name, ns));

        protected virtual void WriteIndexedEndpoints(XmlWriter writer,
            IEnumerable<IndexedEndpoint> endpoints, string name, string ns) =>
                WriteCollection(writer, endpoints, (writer_, endpoint) =>
                    WriteIndexedEndpoint(writer_, endpoint, name, ns));

        static void WriteStringElement(XmlWriter writer, string elName, string elNs, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                writer.WriteStartElement(elName, elNs);
                writer.WriteString(value);
                writer.WriteEndElement();
            }
        }

        void WriteXEncKeySize(XmlWriter writer, int keySize)
        {
            writer.WriteStartElement("KeySize", XEncNs);
            writer.WriteString(keySize.ToString());
            writer.WriteEndElement();
        }

        // md:EncryptionMethod and xenc:EncryptionMethod are virtually identical, but have different namespaces
        // md:EncryptionMethod is not well defined so I've kept it as a separate type for now
        protected virtual void WriteXEncEncryptionMethod(XmlWriter writer, XEncEncryptionMethod method)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            writer.WriteStartElement("EncryptionMethod", XEncNs);
            writer.WriteAttributeString("Algorithm", method.Algorithm.ToString());
            WriteCustomAttributes(writer, method);

            if (method.KeySize != 0)
            {
                WriteXEncKeySize(writer, method.KeySize);
            }
            WriteBase64Element(writer, "OAEPparams", XEncNs, method.OAEPparams);

            WriteCustomElements(writer, method);
            writer.WriteEndElement();
        }

        protected virtual void WriteEncryptionMethod(XmlWriter writer, EncryptionMethod method)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            writer.WriteStartElement("EncryptionMethod", Saml2MetadataNs);
            writer.WriteAttributeString("Algorithm", method.Algorithm.ToString());
            WriteCustomAttributes(writer, method);

            if (method.KeySize != 0)
            {
                WriteXEncKeySize(writer, method.KeySize);
            }
            WriteBase64Element(writer, "OAEPparams", XEncNs, method.OAEPparams);

            WriteCustomElements(writer, method);
            writer.WriteEndElement();
        }

        void WriteCollection<T>(XmlWriter writer, IEnumerable<T> elts, Action<XmlWriter, T> writeHandler)
        {
            foreach (var elt in elts)
            {
                writeHandler(writer, elt);
            }
        }

        protected virtual void WriteRSAKeyValue(XmlWriter writer, RsaKeyValue rsa)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (rsa == null)
            {
                throw new ArgumentNullException(nameof(rsa));
            }

            writer.WriteStartElement("RSAKeyValue", DSigNs);
            WriteCustomAttributes(writer, rsa);

            WriteBase64Element(writer, "Modulus", DSigNs, rsa.Parameters.Modulus);
            WriteBase64Element(writer, "Exponent", DSigNs, rsa.Parameters.Exponent);

            WriteCustomElements(writer, rsa);
            writer.WriteEndElement();
        }

        static byte[] GetIntAsBigEndian(int value)
        {
            byte[] data = new byte[4];
            data[0] = (byte)(((uint)value >> 24) & 0xff);
            data[1] = (byte)(((uint)value >> 16) & 0xff);
            data[2] = (byte)(((uint)value >> 8) & 0xff);
            data[3] = (byte)((uint)value & 0xff);
            return data;
        }

        protected virtual void WriteDSAKeyValue(XmlWriter writer, DsaKeyValue dsa)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (dsa == null)
            {
                throw new ArgumentNullException(nameof(dsa));
            }

            writer.WriteStartElement("DSAKeyValue", DSigNs);
            WriteCustomAttributes(writer, dsa);

            WriteBase64Element(writer, "P", DSigNs, dsa.Parameters.P);
            WriteBase64Element(writer, "Q", DSigNs, dsa.Parameters.Q);
            WriteBase64Element(writer, "G", DSigNs, dsa.Parameters.G);
            WriteBase64Element(writer, "Y", DSigNs, dsa.Parameters.Y);
            WriteBase64Element(writer, "J", DSigNs, dsa.Parameters.J);
            WriteBase64Element(writer, "Seed", DSigNs, dsa.Parameters.Seed);
            WriteBase64Element(writer, "PgenCounter", DSigNs,
                GetIntAsBigEndian(dsa.Parameters.Counter));

            WriteCustomElements(writer, dsa);
            writer.WriteEndElement();
        }

        protected virtual void WriteECKeyValue(XmlWriter writer, EcKeyValue ec)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (ec == null)
            {
                throw new ArgumentNullException(nameof(ec));
            }

            writer.WriteStartElement("ECKeyValue", DSig11Ns);
            WriteCustomAttributes(writer, ec);

            writer.WriteStartElement("NamedCurve", DSig11Ns);
            writer.WriteAttributeString("URI", "urn:oid:" + ec.Parameters.Curve.Oid.Value);
            writer.WriteEndElement();

            byte[] data = new byte[ec.Parameters.Q.X.Length + ec.Parameters.Q.Y.Length + 1];
            data[0] = 4;
            Array.Copy(ec.Parameters.Q.X, 0, data, 1, ec.Parameters.Q.X.Length);
            Array.Copy(ec.Parameters.Q.Y, 0, data, 1 + ec.Parameters.Q.X.Length,
                ec.Parameters.Q.Y.Length);
            WriteBase64Element(writer, "PublicKey", DSig11Ns, data);

            WriteCustomElements(writer, ec);
            writer.WriteEndElement();
        }

        protected virtual void WriteKeyValue(XmlWriter writer, KeyValue keyValue)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (keyValue == null)
            {
                throw new ArgumentNullException(nameof(keyValue));
            }
            writer.WriteStartElement("KeyValue", DSigNs);
            WriteCustomAttributes(writer, keyValue);
            if (keyValue is RsaKeyValue rsa)
            {
                WriteRSAKeyValue(writer, rsa);
            }
            else if (keyValue is DsaKeyValue dsa)
            {
                WriteDSAKeyValue(writer, dsa);
            }
            else if (keyValue is EcKeyValue ec)
            {
                WriteECKeyValue(writer, ec);
            }
            WriteCustomElements(writer, keyValue);
            writer.WriteEndElement();
        }

        protected virtual void WriteRetrievalMethod(XmlWriter writer, RetrievalMethod method)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            writer.WriteStartElement("RetrievalMethod", DSigNs);
            WriteUriAttributeIfPresent(writer, "URI", null, method.Uri);
            WriteUriAttributeIfPresent(writer, "Type", null, method.Type);
            WriteCustomAttributes(writer, method);

            WriteWrappedElements(writer, "ds", "Transforms", DSigNs, method.Transforms);

            WriteCustomElements(writer, method);
            writer.WriteEndElement();
        }

        protected virtual void WriteX509IssuerSerial(XmlWriter writer, X509IssuerSerial issuerSerial)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (issuerSerial == null)
            {
                throw new ArgumentNullException(nameof(issuerSerial));
            }

            writer.WriteStartElement("X509IssuerSerial", DSigNs);
            WriteCustomAttributes(writer, issuerSerial);
            WriteStringElement(writer, "X509IssuerName", DSigNs, issuerSerial.Name);
            WriteStringElement(writer, "X509SerialNumber", DSigNs, issuerSerial.Serial);
            WriteCustomElements(writer, issuerSerial);
            writer.WriteEndElement();
        }

        protected virtual void WriteX509Digest(XmlWriter writer, X509Digest digest)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (digest == null)
            {
                throw new ArgumentNullException(nameof(digest));
            }

            writer.WriteStartElement("X509Digest", DSigNs);
            writer.WriteAttributeString("Algorithm", digest.Algorithm.ToString());
            WriteCustomAttributes(writer, digest);
            writer.WriteBase64(digest.Value, 0, digest.Value.Length);
            WriteCustomElements(writer, digest);
            writer.WriteEndElement();
        }

        protected virtual void WriteX509Data(XmlWriter writer, X509Data data)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            writer.WriteStartElement("X509Data", DSigNs);
            WriteCustomAttributes(writer, data);
            if (data.IssuerSerial != null)
            {
                WriteX509IssuerSerial(writer, data.IssuerSerial);
            }
            if (data.SKI != null)
            {
                WriteBase64Element(writer, "X509SKI", DSigNs, data.SKI);
            }
            WriteStringElementIfPresent(writer, "X509SubjectName", DSigNs, data.SubjectName);
            foreach (var cert in data.Certificates)
            {
                WriteBase64Element(writer, "X509Certificate", DSigNs, cert.GetRawCertData());
            }
            if (data.CRL != null)
            {
                WriteBase64Element(writer, "X509CRL", DSigNs, data.CRL);
            }
            if (data.Digest != null)
            {
                WriteX509Digest(writer, data.Digest);
            }

            WriteCustomElements(writer, data);
            writer.WriteEndElement();
        }

        protected virtual void WriteKeyData(XmlWriter writer, KeyData keyData)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (keyData == null)
            {
                throw new ArgumentNullException(nameof(keyData));
            }
            if (keyData is X509Data x509Data)
            {
                WriteX509Data(writer, x509Data);
            }
        }

        protected virtual void WriteDSigKeyInfo(XmlWriter writer, DSigKeyInfo keyInfo)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (keyInfo == null)
            {
                throw new ArgumentNullException(nameof(keyInfo));
            }

            writer.WriteStartElement("KeyInfo", DSigNs);
            WriteStringAttributeIfPresent(writer, "Id", null, keyInfo.Id);
            WriteCustomAttributes(writer, keyInfo);

            WriteStringElements(writer, "KeyName", DSigNs, keyInfo.KeyNames);
            WriteCollection(writer, keyInfo.KeyValues, WriteKeyValue);
            WriteCollection(writer, keyInfo.RetrievalMethods, WriteRetrievalMethod);
            WriteCollection(writer, keyInfo.Data, WriteKeyData);

            WriteCustomElements(writer, keyInfo);
            writer.WriteEndElement();
        }

        protected virtual void WriteCipherReference(XmlWriter writer, CipherReference reference)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            writer.WriteStartElement("CipherReference", XEncNs);
            WriteUriAttributeIfPresent(writer, "URI", null, reference.Uri);
            WriteCustomAttributes(writer, reference);

            WriteWrappedElements(writer, "xenc", "Transforms", XEncNs, reference.Transforms);
            WriteCustomElements(writer, reference);
            writer.WriteEndElement();
        }

        protected virtual void WriteCipherData(XmlWriter writer, CipherData data)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            writer.WriteStartElement("CipherData", XEncNs);
            WriteCustomAttributes(writer, data);
            if (data.CipherValue != null)
            {
                WriteStringElement(writer, "CipherValue", XEncNs, data.CipherValue);
            }
            if (data.CipherReference != null)
            {
                WriteCipherReference(writer, data.CipherReference);
            }
            WriteCustomElements(writer, data);
            writer.WriteEndElement();
        }

        // <element name="EncryptionProperty" type="xenc:EncryptionPropertyType"/> 
        // 
        // <complexType name="EncryptionPropertyType" mixed="true">
        //   <choice maxOccurs="unbounded">
        //     <any namespace="##other" processContents="lax"/>
        //   </choice>
        //   <attribute name="Target" type="anyURI" use="optional"/> 
        //   <attribute name="Id" type="ID" use="optional"/> 
        //   <anyAttribute namespace="http://www.w3.org/XML/1998/namespace"/>
        // </complexType>
        protected virtual void WriteEncryptionProperty(XmlWriter writer, EncryptionProperty property)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            writer.WriteStartElement("EncryptionProperty", XEncNs);
            WriteUriAttributeIfPresent(writer, "Target", null, property.Target);
            WriteStringAttributeIfPresent(writer, "Id", null, property.Id);
            WriteCustomAttributes(writer, property);
            WriteCustomElements(writer, property);
            writer.WriteEndElement();
        }

        protected virtual void WriteEncryptionProperties(XmlWriter writer, EncryptionProperties properties)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }
            writer.WriteStartElement("EncryptionProperties", XEncNs);
            WriteStringAttributeIfPresent(writer, "Id", null, properties.Id);
            WriteCustomAttributes(writer, properties);
            WriteCollection(writer, properties.Properties, WriteEncryptionProperty);
            WriteCustomElements(writer, properties);
            writer.WriteEndElement();
        }

        protected virtual void WriteEncryptedData(XmlWriter writer, EncryptedData data)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            writer.WriteStartElement("EncryptedData", XEncNs);
            WriteStringAttributeIfPresent(writer, "Id", null, data.Id);
            WriteUriAttributeIfPresent(writer, "Type", null, data.Type);
            WriteStringAttributeIfPresent(writer, "MimeType", null, data.MimeType);
            WriteUriAttributeIfPresent(writer, "Encoding", null, data.Encoding);
            WriteCustomAttributes(writer, data);

            if (data.EncryptionMethod != null)
            {
                WriteXEncEncryptionMethod(writer, data.EncryptionMethod);
            }
            if (data.KeyInfo != null)
            {
                WriteDSigKeyInfo(writer, data.KeyInfo);
            }
            if (data.CipherData != null)
            {
                WriteCipherData(writer, data.CipherData);
            }
            if (data.EncryptionProperties != null)
            {
                WriteEncryptionProperties(writer, data.EncryptionProperties);
            }

            WriteCustomElements(writer, data);
            writer.WriteEndElement();
        }

        protected virtual void WriteEncryptedValue(XmlWriter writer, EncryptedValue value)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteStartElement("EncryptedValue", AuthNs);
            if (value.DecryptionCondition != null)
            {
                writer.WriteAttributeString("DecryptionCondition", value.DecryptionCondition.ToString());
            }
            WriteCustomAttributes(writer, value);

            WriteEncryptedData(writer, value.EncryptedData);

            WriteCustomElements(writer, value);
            writer.WriteEndElement();
        }

        protected virtual void WriteClaimValue(XmlWriter writer, ClaimValue value)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!string.IsNullOrEmpty(value.Value) && value.StructuredValue != null)
            {
                throw new MetadataSerializationException(
                    "Invalid claim value that has both Value and StructuredValue properties set");
            }
            if (value.Value == null && value.StructuredValue == null)
            {
                throw new MetadataSerializationException(
                    "Invalid claim value that has neither Value nor StructuredValue properties set");
            }

            if (value.Value != null)
            {
                WriteStringElement(writer, "Value", AuthNs, value.Value);
            }
            else
            {
                writer.WriteStartElement("StructuredValue", AuthNs);
                foreach (var elt in value.StructuredValue)
                {
                    elt.WriteTo(writer);
                }
                writer.WriteEndElement();
            }
        }

        protected virtual void WriteCompareConstraint(XmlWriter writer,
            ConstrainedValue.CompareConstraint constraint)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (constraint == null)
            {
                throw new ArgumentNullException(nameof(constraint));
            }

            string elName;
            switch (constraint.CompareOp)
            {
                case ConstrainedValue.CompareConstraint.CompareOperator.Lt:
                    elName = "ValueLessThan";
                    break;
                case ConstrainedValue.CompareConstraint.CompareOperator.Lte:
                    elName = "ValueLessThanOrEqual";
                    break;
                case ConstrainedValue.CompareConstraint.CompareOperator.Gt:
                    elName = "ValueGreaterThan";
                    break;
                case ConstrainedValue.CompareConstraint.CompareOperator.Gte:
                    elName = "ValueGreaterThanOrEqual";
                    break;
                default:
                    throw new MetadataSerializationException(
                        $"Unknown constrained value compare operator '{constraint.CompareOp}'");
            }

            writer.WriteStartElement(elName, AuthNs);
            WriteCustomAttributes(writer, constraint);
            WriteClaimValue(writer, constraint.Value);
            WriteCustomElements(writer, constraint);
            writer.WriteEndElement();
        }

        protected virtual void WriteListContraint(XmlWriter writer,
            ConstrainedValue.ListConstraint constraint)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (constraint == null)
            {
                throw new ArgumentNullException(nameof(constraint));
            }

            writer.WriteStartElement("ValueOneOf", AuthNs);
            WriteCustomAttributes(writer, constraint);
            foreach (var value in constraint.Values)
            {
                WriteClaimValue(writer, value);
            }
            WriteCustomElements(writer, constraint);
            writer.WriteEndElement();
        }

        protected virtual void WriteRangeConstraint(XmlWriter writer,
            ConstrainedValue.RangeConstraint constraint)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (constraint == null)
            {
                throw new ArgumentNullException(nameof(constraint));
            }

            writer.WriteStartElement("ValueInRange", AuthNs);
            WriteCustomAttributes(writer, constraint);
            if (constraint.UpperBound != null)
            {
                writer.WriteStartElement("ValueUpperBound", AuthNs);
                WriteClaimValue(writer, constraint.UpperBound);
                writer.WriteEndElement();
            }
            if (constraint.LowerBound != null)
            {
                writer.WriteStartElement("ValueLowerBound", AuthNs);
                WriteClaimValue(writer, constraint.LowerBound);
                writer.WriteEndElement();
            }
            WriteCustomElements(writer, constraint);
            writer.WriteEndElement();
        }

        protected virtual void WriteListConstraint(XmlWriter writer,
            ConstrainedValue.ListConstraint constraint)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (constraint == null)
            {
                throw new ArgumentNullException(nameof(constraint));
            }

            writer.WriteStartElement("ValueOneOf", AuthNs);
            WriteCustomAttributes(writer, constraint);
            foreach (var value in constraint.Values)
            {
                WriteClaimValue(writer, value);
            }
            WriteCustomElements(writer, constraint);
            writer.WriteEndElement();
        }

        protected virtual void WriteConstrainedValue(XmlWriter writer, ConstrainedValue value)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            writer.WriteStartElement("ConstrainedValue", AuthNs);
            WriteBooleanAttribute(writer, "AssertConstraint", null, value.AssertConstraint);
            WriteCustomAttributes(writer, value);
            foreach (var constraint in value.Constraints)
            {
                if (constraint is ConstrainedValue.CompareConstraint cc)
                {
                    WriteCompareConstraint(writer, cc);
                }
                else if (constraint is ConstrainedValue.ListConstraint lc)
                {
                    WriteListConstraint(writer, lc);
                }
                else if (constraint is ConstrainedValue.RangeConstraint rc)
                {
                    WriteRangeConstraint(writer, rc);
                }
                else
                {
                    throw new MetadataSerializationException(
                        $"Unknown constraint type '{constraint.GetType()}'");
                }
            }
            WriteCustomElements(writer, value);
            writer.WriteEndElement();
        }

        protected virtual void WriteDisplayClaim(XmlWriter writer, DisplayClaim claim)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }

            writer.WriteStartElement("ClaimType", AuthNs);
            writer.WriteAttributeString("Uri", claim.ClaimType);
            WriteBooleanAttribute(writer, "Optional", null, claim.Optional);
            WriteCustomAttributes(writer, claim);

            WriteStringElement(writer, "DisplayName", AuthNs, claim.DisplayName);
            WriteStringElement(writer, "Description", AuthNs, claim.Description);
            WriteStringElement(writer, "DisplayValue", AuthNs, claim.DisplayValue);
            WriteStringElement(writer, "Value", AuthNs, claim.Value);
            if (claim.StructuredValue != null)
            {
                writer.WriteStartElement("StructuredValue", AuthNs);
                foreach (var elt in claim.StructuredValue)
                {
                    elt.WriteTo(writer);
                }
                writer.WriteEndElement();
            }
            if (claim.EncryptedValue != null)
            {
                WriteEncryptedValue(writer, claim.EncryptedValue);
            }
            if (claim.ConstrainedValue != null)
            {
                WriteConstrainedValue(writer, claim.ConstrainedValue);
            }
            WriteCustomElements(writer, claim);
            writer.WriteEndElement();
        }

        static void WriteCachedMetadataAttributes(XmlWriter writer, ICachedMetadata cachedMetadata)
        {
            if (cachedMetadata.CacheDuration.HasValue)
            {
                writer.WriteAttributeString("cacheDuration",
                    cachedMetadata.CacheDuration.Value.ToString());
            }
            if (cachedMetadata.ValidUntil.HasValue)
            {
                writer.WriteAttributeString("validUntil",
                    XmlConvert.ToString(cachedMetadata.ValidUntil.Value, XmlDateTimeSerializationMode.Utc));
            }
        }

        protected virtual void WriteEntitiesDescriptor(XmlWriter writer, EntitiesDescriptor entitiesDescriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (entitiesDescriptor == null)
            {
                throw new ArgumentNullException(nameof(entitiesDescriptor));
            }

            EnvelopedSignatureWriter signatureWriter = null;
            if (entitiesDescriptor.SigningCredentials != null)
            {
                string referenceId = Guid.NewGuid().ToString("N");
                signatureWriter = new EnvelopedSignatureWriter(writer,
                    entitiesDescriptor.SigningCredentials, referenceId);
                writer = signatureWriter;
            }

            writer.WriteStartElement("EntitiesDescriptor", Saml2MetadataNs);
            WriteCachedMetadataAttributes(writer, entitiesDescriptor);
            WriteStringAttributeIfPresent(writer, "ID", null, entitiesDescriptor.Id);
            WriteStringAttributeIfPresent(writer, "Name", null, entitiesDescriptor.Name);
            WriteCustomAttributes(writer, entitiesDescriptor);

            if (signatureWriter != null)
            {
                signatureWriter.WriteSignature();
            }

            WriteWrappedElements(writer, null, "Extensions", Saml2MetadataNs,
                entitiesDescriptor.Extensions);

            foreach (var childEntity in entitiesDescriptor.ChildEntities)
            {
                WriteEntityDescriptor(writer, childEntity);
            }

            foreach (var childEntityDescriptor in entitiesDescriptor.ChildEntityGroups)
            {
                WriteEntitiesDescriptor(writer, childEntityDescriptor);
            }

            WriteCustomElements(writer, entitiesDescriptor);
            writer.WriteEndElement();
        }

        protected virtual void WriteNameIDFormat(XmlWriter writer, NameIDFormat nameIDFormat)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (nameIDFormat == null)
            {
                throw new ArgumentNullException(nameof(nameIDFormat));
            }
            writer.WriteStartElement("NameIDFormat", Saml2MetadataNs);
            WriteCustomAttributes(writer, nameIDFormat);
            writer.WriteString(nameIDFormat.Uri.ToString());
            WriteCustomElements(writer, nameIDFormat);
            writer.WriteEndElement();
        }

        protected virtual void WriteAuthnAuthorityDescriptor(XmlWriter writer, AuthnAuthorityDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }
            writer.WriteStartElement("AuthnAuthorityDescriptor", Saml2MetadataNs);
            WriteRoleDescriptorAttributes(writer, descriptor);
            WriteCustomAttributes(writer, descriptor);
            WriteRoleDescriptorElements(writer, descriptor);

            WriteEndpoints(writer, descriptor.AuthnQueryServices,
                "AuthnQueryService", Saml2MetadataNs);
            WriteEndpoints(writer, descriptor.AssertionIdRequestServices,
                "AssertionIDRequestService", Saml2MetadataNs);
            WriteCollection(writer, descriptor.NameIDFormats, WriteNameIDFormat);

            WriteCustomElements(writer, descriptor);
            writer.WriteEndElement();
        }

        protected virtual void WriteAttributeProfile(XmlWriter writer, AttributeProfile profile)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            writer.WriteStartElement("AttributeProfile", Saml2MetadataNs);
            WriteCustomAttributes(writer, profile);
            writer.WriteString(profile.Uri.ToString());
            WriteCustomElements(writer, profile);
            writer.WriteEndElement();
        }

        protected virtual void WriteAttributeAuthorityDescriptor(XmlWriter writer, AttributeAuthorityDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }
            writer.WriteStartElement("AttributeAuthorityDescriptor", Saml2MetadataNs);
            WriteRoleDescriptorAttributes(writer, descriptor);
            WriteCustomAttributes(writer, descriptor);
            WriteRoleDescriptorElements(writer, descriptor);

            foreach (var service in descriptor.AttributeServices)
            {
                WriteEndpoint(writer, service, "AttributeService", Saml2MetadataNs);
            }

            foreach (var ars in descriptor.AssertionIdRequestServices)
            {
                WriteEndpoint(writer, ars, "AssertionIDRequestService", Saml2MetadataNs);
            }

            foreach (var nameIDFormat in descriptor.NameIDFormats)
            {
                WriteNameIDFormat(writer, nameIDFormat);
            }

            foreach (var attributeProfile in descriptor.AttributeProfiles)
            {
                WriteAttributeProfile(writer, attributeProfile);
            }

            foreach (var attribute in descriptor.Attributes)
            {
                WriteAttribute(writer, attribute);
            }

            WriteCustomElements(writer, descriptor);
            writer.WriteEndElement();
        }

        protected virtual void WritePDPDescriptor(XmlWriter writer, PDPDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }
            writer.WriteStartElement("PDPDescriptor", Saml2MetadataNs);
            WriteRoleDescriptorAttributes(writer, descriptor);
            WriteCustomAttributes(writer, descriptor);

            WriteRoleDescriptorElements(writer, descriptor);
            WriteEndpoints(writer, descriptor.AuthzServices,
                "AuthzService", Saml2MetadataNs);
            WriteEndpoints(writer, descriptor.AssertionIdRequestServices,
                "AssertionIDRequestService", Saml2MetadataNs);
            foreach (var nameIdFormat in descriptor.NameIDFormats)
            {
                WriteNameIDFormat(writer, nameIdFormat);
            }

            WriteCustomElements(writer, descriptor);
            writer.WriteEndElement();
        }

        protected virtual void WriteAffiliationDescriptor(XmlWriter writer, AffiliationDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            writer.WriteStartElement("AffiliationDescriptor", Saml2MetadataNs);
            writer.WriteAttributeString("affiliationOwnerID", descriptor.AffiliationOwnerId.Id);
            WriteCachedMetadataAttributes(writer, descriptor);
            WriteStringAttributeIfPresent(writer, "ID", null, descriptor.Id);
            WriteCustomAttributes(writer, descriptor);

            WriteWrappedElements(writer, "md", "Extensions", Saml2MetadataNs, descriptor.Extensions);
            WriteCollection(writer, descriptor.AffiliateMembers, (writer_, member) =>
            {
                WriteStringElement(writer_, "AffiliateMember", Saml2MetadataNs, member.Id);
            });
            WriteCollection(writer, descriptor.KeyDescriptors, WriteKeyDescriptor);

            WriteCustomElements(writer, descriptor);
            writer.WriteEndElement();
        }

        protected virtual void WriteAdditionalMetadataLocation(XmlWriter writer, AdditionalMetadataLocation location)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            writer.WriteStartElement("AdditionalMetadataLocation", Saml2MetadataNs);
            WriteCustomAttributes(writer, location);
            writer.WriteAttributeString("namespace", location.Namespace);
            writer.WriteString(location.Uri.ToString());
            WriteCustomElements(writer, location);
            writer.WriteEndElement();
        }

        protected virtual void WriteEntityDescriptor(XmlWriter writer, EntityDescriptor entityDescriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (entityDescriptor == null)
            {
                throw new ArgumentNullException(nameof(entityDescriptor));
            }

            EnvelopedSignatureWriter signatureWriter = null;
            if (entityDescriptor.SigningCredentials != null)
            {
                string referenceId = Guid.NewGuid().ToString("N");
                signatureWriter = new EnvelopedSignatureWriter(writer,
                    entityDescriptor.SigningCredentials, referenceId);
            }

            writer.WriteStartElement("EntityDescriptor", Saml2MetadataNs);
            WriteCachedMetadataAttributes(writer, entityDescriptor);
            WriteStringAttributeIfPresent(writer, "ID", null, entityDescriptor.Id);
            writer.WriteAttributeString("entityID", entityDescriptor.EntityId.Id);
            WriteCustomAttributes(writer, entityDescriptor);

            if (signatureWriter != null)
            {
                signatureWriter.WriteSignature();
            }

            WriteWrappedElements(writer, null, "Extensions", Saml2MetadataNs,
                entityDescriptor.Extensions);

            foreach (var roleDescriptor in entityDescriptor.RoleDescriptors)
            {
                if (roleDescriptor is ApplicationServiceDescriptor appDescriptor)
                {
                    WriteApplicationServiceDescriptor(writer, appDescriptor);
                }
                else if (roleDescriptor is SecurityTokenServiceDescriptor secDescriptor)
                {
                    WriteSecurityTokenServiceDescriptor(writer, secDescriptor);
                }
                else if (roleDescriptor is IdpSsoDescriptor idpSsoDescriptor)
                {
                    WriteIdpSsoDescriptor(writer, idpSsoDescriptor);
                }
                else if (roleDescriptor is SpSsoDescriptor spSsoDescriptor)
                {
                    WriteSpSsoDescriptor(writer, spSsoDescriptor);
                }
                else if (roleDescriptor is AuthnAuthorityDescriptor authDescriptor)
                {
                    WriteAuthnAuthorityDescriptor(writer, authDescriptor);
                }
                else if (roleDescriptor is AttributeAuthorityDescriptor attDescriptor)
                {
                    WriteAttributeAuthorityDescriptor(writer, attDescriptor);
                }
                else if (roleDescriptor is PDPDescriptor pdpDescriptor)
                {
                    WritePDPDescriptor(writer, pdpDescriptor);
                }
            }
            foreach (AffiliationDescriptor affDescriptor in entityDescriptor.AffiliationDescriptors)
            {
                WriteAffiliationDescriptor(writer, affDescriptor);
            }
            if (entityDescriptor.Organization != null)
            {
                WriteOrganization(writer, entityDescriptor.Organization);
            }
            foreach (var person in entityDescriptor.Contacts)
            {
                WriteContactPerson(writer, person);
            }
            foreach (var meta in entityDescriptor.AdditionalMetadataLocations)
            {
                WriteAdditionalMetadataLocation(writer, meta);
            }
            WriteCustomElements(writer, entityDescriptor);
            writer.WriteEndElement();
        }

        protected virtual void WriteIdpSsoDescriptor(XmlWriter writer, IdpSsoDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            writer.WriteStartElement("IDPSSODescriptor", Saml2MetadataNs);
            WriteBooleanAttribute(writer, "WantAuthnRequestsSigned", null, descriptor.WantAuthnRequestsSigned);
            WriteSsoDescriptorAttributes(writer, descriptor);
            WriteCustomAttributes(writer, descriptor);

            WriteSsoDescriptorElements(writer, descriptor);
            WriteEndpoints(writer, descriptor.SingleSignOnServices,
                "SingleSignOnService", Saml2MetadataNs);
            WriteEndpoints(writer, descriptor.NameIDMappingServices,
                "NameIDMappingService", Saml2MetadataNs);
            WriteEndpoints(writer, descriptor.AssertionIDRequestServices,
                "AssertionIDRequestService", Saml2MetadataNs);
            foreach (var attProfile in descriptor.AttributeProfiles)
            {
                WriteAttributeProfile(writer, attProfile);
            }
            foreach (var attribute in descriptor.SupportedAttributes)
            {
                WriteAttribute(writer, attribute);
            }

            WriteCustomElements(writer, descriptor);
            writer.WriteEndElement();
        }

        protected virtual void WriteKeyDescriptor(XmlWriter writer, KeyDescriptor keyDescriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (keyDescriptor == null)
            {
                throw new ArgumentNullException(nameof(keyDescriptor));
            }

            writer.WriteStartElement("KeyDescriptor", Saml2MetadataNs);
            if (keyDescriptor.Use != KeyType.Unspecified)
            {
                string useValue;
                switch (keyDescriptor.Use)
                {
                    case KeyType.Signing:
                        useValue = "signing";
                        break;
                    case KeyType.Encryption:
                        useValue = "encryption";
                        break;
                    default:
                        throw new MetadataSerializationException(
                            $"Unknown KeyType enumeration entry '{keyDescriptor.Use}'");
                }
                writer.WriteAttributeString("use", useValue);
            }
            WriteCustomAttributes(writer, keyDescriptor);

            if (keyDescriptor.KeyInfo != null)
            {
                WriteDSigKeyInfo(writer, keyDescriptor.KeyInfo);
            }

            WriteCollection(writer, keyDescriptor.EncryptionMethods, WriteEncryptionMethod);

            WriteCustomElements(writer, keyDescriptor);
            writer.WriteEndElement();
        }

        protected virtual void WriteLocalizedName(XmlWriter writer, LocalizedName name,
            string elName, string ns)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (elName == null)
            {
                throw new ArgumentNullException(nameof(elName));
            }
            if (ns == null)
            {
                throw new ArgumentNullException(nameof(ns));
            }

            writer.WriteStartElement(elName, ns);
            writer.WriteAttributeString("xml", "lang", XmlNs, name.Language);
            WriteCustomAttributes(writer, name);
            writer.WriteString(name.Name);
            WriteCustomElements(writer, name);
            writer.WriteEndElement();
        }

        void WriteLocalizedNames(XmlWriter writer, IEnumerable<LocalizedName> names,
            string elName, string ns) =>
                WriteCollection(writer, names, (writer_, name) =>
                    WriteLocalizedName(writer_, name, elName, ns));

        protected virtual void WriteLocalizedUri(XmlWriter writer, LocalizedUri uri,
            string name, string ns)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (ns == null)
            {
                throw new ArgumentNullException(nameof(ns));
            }

            writer.WriteStartElement(name, ns);
            writer.WriteAttributeString("xml", "lang", XmlNs, uri.Language);
            WriteCustomAttributes(writer, name);
            writer.WriteString(uri.Uri.ToString());
            WriteCustomElements(writer, name);
            writer.WriteEndElement();
        }

        public void WriteMetadata(Stream stream, MetadataBase metadata)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }
            using (var writer = XmlWriter.Create(stream))
            {
                WriteMetadata(writer, metadata);
            }
        }

        public void WriteMetadata(XmlWriter writer, MetadataBase metadata)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }
            WriteMetadataCore(writer, metadata);
        }

        protected virtual void WriteMetadataCore(XmlWriter writer, MetadataBase metadata)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            if (metadata is EntitiesDescriptor entities)
            {
                WriteEntitiesDescriptor(writer, entities);
            }
            else if (metadata is EntityDescriptor entity)
            {
                WriteEntityDescriptor(writer, entity);
            }
        }

        protected virtual void WriteOrganization(XmlWriter writer, Organization organization)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (organization == null)
            {
                throw new ArgumentNullException(nameof(organization));
            }
            if (organization.Names.Count == 0)
            {
                throw new MetadataSerializationException(
                    "An organisation must have at least one Name property");
            }
            if (organization.DisplayNames.Count == 0)
            {
                throw new MetadataSerializationException(
                    "An organisation must have at least one DisplayName property");
            }
            if (organization.Urls.Count == 0)
            {
                throw new MetadataSerializationException(
                    "An organisation must have at least one Url property");
            }

            writer.WriteStartElement("Organization", Saml2MetadataNs);
            WriteCustomAttributes(writer, organization);

            if (organization.Extensions.Count > 0)
            {
                writer.WriteStartElement("Extensions", Saml2MetadataNs);
                foreach (var extension in organization.Extensions)
                {
                    extension.WriteTo(writer);
                }
                writer.WriteEndElement();
            }
            WriteLocalizedNames(writer, organization.Names,
                "OrganizationName", Saml2MetadataNs);
            WriteLocalizedNames(writer, organization.DisplayNames,
                "OrganizationDisplayName", Saml2MetadataNs);
            WriteCollection(writer, organization.Urls, (writer_, uri) =>
                WriteLocalizedUri(writer_, uri, "OrganizationURL", Saml2MetadataNs));

            WriteCustomElements(writer, organization);
            writer.WriteEndElement();
        }

        protected virtual void WriteRoleDescriptorAttributes(XmlWriter writer, RoleDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            WriteStringAttributeIfPresent(writer, "ID", null, descriptor.Id);
            WriteUriAttributeIfPresent(writer, "errorURL", null, descriptor.ErrorUrl);
            WriteCachedMetadataAttributes(writer, descriptor);
            string protocolsSupported = descriptor.ProtocolsSupported.Aggregate("",
                (list, uri) => $"{list}{(list == "" ? "" : " ")}{uri}");
            writer.WriteAttributeString("protocolSupportEnumeration", protocolsSupported);
            WriteCustomAttributes(writer, descriptor);
        }

        void WriteRoleDescriptorElements(XmlWriter writer, RoleDescriptor descriptor, bool writeExtensions)
        {
            if (writeExtensions)
            {
                WriteWrappedElements(writer, null, "Extensions", Saml2MetadataNs,
                    descriptor.Extensions);
            }
            foreach (var kd in descriptor.Keys)
            {
                WriteKeyDescriptor(writer, kd);
            }
            if (descriptor.Organization != null)
            {
                WriteOrganization(writer, descriptor.Organization);
            }
            foreach (var contact in descriptor.Contacts)
            {
                WriteContactPerson(writer, contact);
            }
            WriteCustomElements(writer, descriptor);
        }

        protected virtual void WriteRoleDescriptorElements(XmlWriter writer, RoleDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }
            WriteRoleDescriptorElements(writer, descriptor, true);
        }

        protected virtual void WriteSecurityTokenServiceDescriptor(XmlWriter writer,
            SecurityTokenServiceDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            writer.WriteStartElement("RoleDescriptor", Saml2MetadataNs);
            writer.WriteAttributeString("xsi", "type", XsiNs, "fed:SecurityTokenServiceType");
            WriteWebServiceDescriptorAttributes(writer, descriptor);
            WriteCustomAttributes(writer, descriptor);

            WriteWebServiceDescriptorElements(writer, descriptor);
            WriteEndpointReferences(writer, "SecurityTokenServiceEndpoint",
                FedNs, descriptor.SecurityTokenServiceEndpoints);
            WriteEndpointReferences(writer, "SingleSignOutSubscriptionEndpoint",
                FedNs, descriptor.SingleSignOutSubscriptionEndpoints);
            WriteEndpointReferences(writer, "SingleSignOutNotificationEndpoint",
                FedNs, descriptor.SingleSignOutNotificationEndpoints);
            WriteEndpointReferences(writer, "PassiveRequestorEndpoint",
                FedNs, descriptor.PassiveRequestorEndpoints);

            WriteCustomElements(writer, descriptor);

            writer.WriteEndElement();
        }

        protected virtual void WriteRequestedAttribute(XmlWriter writer, RequestedAttribute attribute)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            writer.WriteStartElement("RequestedAttribute", Saml2MetadataNs);
            WriteBooleanAttribute(writer, "isRequired", null, attribute.IsRequired);
            WriteAttributeAttributes(writer, attribute);
            WriteAttributeElements(writer, attribute);
            writer.WriteEndElement();
        }

        protected virtual void WriteAttributeConsumingService(XmlWriter writer, AttributeConsumingService service)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            writer.WriteStartElement("AttributeConsumingService", Saml2MetadataNs);
            writer.WriteAttributeString("index", service.Index.ToString());
            WriteBooleanAttribute(writer, "isDefault", null, service.IsDefault);
            WriteCustomAttributes(writer, service);

            WriteLocalizedNames(writer, service.ServiceNames,
                "ServiceName", Saml2MetadataNs);
            WriteLocalizedNames(writer, service.ServiceDescriptions,
                "ServiceDescription", Saml2MetadataNs);
            WriteCollection(writer, service.RequestedAttributes,
                WriteRequestedAttribute);
            WriteCustomElements(writer, service);
            writer.WriteEndElement();
        }

        protected virtual void WriteSpSsoDescriptor(XmlWriter writer, SpSsoDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            writer.WriteStartElement("SPSSODescriptor", Saml2MetadataNs);
            WriteBooleanAttribute(writer, "AuthnRequestsSigned", null, descriptor.AuthnRequestsSigned);
            WriteBooleanAttribute(writer, "WantAssertionsSigned", null, descriptor.WantAssertionsSigned);
            WriteSsoDescriptorAttributes(writer, descriptor);
            WriteCustomAttributes(writer, descriptor);

            if (descriptor.DiscoveryResponses.Count > 0 || descriptor.Extensions.Any())
            {
                writer.WriteStartElement("Extensions", Saml2MetadataNs);
                WriteIndexedEndpoints(writer, descriptor.DiscoveryResponses.Values,
                    "DiscoveryResponse", IdpDiscNs);
                foreach (var extension in descriptor.Extensions)
                {
                    extension.WriteTo(writer);
                }
                writer.WriteEndElement();
            }
            WriteSsoDescriptorElements(writer, descriptor, false);
            WriteIndexedEndpoints(writer, descriptor.AssertionConsumerServices.Values,
                "AssertionConsumerService", Saml2MetadataNs);
            WriteCollection(writer, descriptor.AttributeConsumingServices.Values,
                WriteAttributeConsumingService);

            WriteCustomElements(writer, descriptor);
            writer.WriteEndElement();
        }

        protected virtual void WriteSsoDescriptorAttributes(XmlWriter writer, SsoDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }
            WriteRoleDescriptorAttributes(writer, descriptor);
            WriteCustomAttributes(writer, descriptor);
        }

        void WriteSsoDescriptorElements(XmlWriter writer, SsoDescriptor descriptor, bool writeExtensions)
        {
            WriteRoleDescriptorElements(writer, descriptor, writeExtensions);
            WriteEndpoints(writer, descriptor.SingleLogoutServices,
                "SingleLogoutService", Saml2MetadataNs);
            WriteEndpoints(writer, descriptor.ManageNameIDServices,
                "ManageNameIDService", Saml2MetadataNs);
            WriteCollection(writer, descriptor.NameIdentifierFormats, WriteNameIDFormat);
            WriteCustomElements(writer, descriptor);
        }

        protected virtual void WriteSsoDescriptorElements(XmlWriter writer, SsoDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            WriteSsoDescriptorElements(writer, descriptor, true);
        }

        void WriteUris(XmlWriter writer, string parentElementName,
            string childElementName, string ns, IEnumerable<Uri> uris)
        {
            if (!uris.Any())
            {
                return;
            }
            writer.WriteStartElement(parentElementName, ns);
            foreach (var uri in uris)
            {
                writer.WriteStartElement(childElementName, ns);
                writer.WriteAttributeString("Uri", uri.ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        protected virtual void WriteWebServiceDescriptorAttributes(XmlWriter writer, WebServiceDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            WriteRoleDescriptorAttributes(writer, descriptor);
            WriteStringAttributeIfPresent(writer, "ServiceDisplayName",
                null, descriptor.ServiceDisplayName);
            WriteStringAttributeIfPresent(writer, "ServiceDescription",
                null, descriptor.ServiceDescription);
        }

        void WriteDisplayClaims(XmlWriter writer, string parentName, string parentNs,
            IEnumerable<DisplayClaim> claims)
        {
            if (!claims.Any())
            {
                return;
            }

            writer.WriteStartElement(parentName, parentNs);
            WriteCollection(writer, claims, WriteDisplayClaim);
            writer.WriteEndElement();
        }

        protected virtual void WriteWebServiceDescriptorElements(XmlWriter writer, WebServiceDescriptor descriptor)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            WriteRoleDescriptorElements(writer, descriptor);

            WriteUris(writer, "LogicalServiceNamesOffered",
                "IssuerName", FedNs, descriptor.LogicalServiceNamesOffered);
            WriteUris(writer, "TokenTypesOffered",
                "TokenType", FedNs, descriptor.TokenTypesOffered);
            WriteUris(writer, "ClaimDialectsOffered",
                "ClaimDialect", FedNs, descriptor.ClaimDialectsOffered);

            WriteDisplayClaims(writer, "ClaimTypesOffered", FedNs,
                descriptor.ClaimTypesOffered);
            WriteDisplayClaims(writer, "ClaimTypesRequested", FedNs,
                descriptor.ClaimTypesRequested);

            if (descriptor.AutomaticPseudonyms.HasValue)
            {
                writer.WriteStartElement("AutomaticPseudonyms", FedNs);
                writer.WriteString(descriptor.AutomaticPseudonyms.Value ? "true" : "false");
                writer.WriteEndElement();
            }
            if (descriptor.TargetScopes.Count > 0)
            {
                writer.WriteStartElement("TargetScopes", FedNs);
                WriteCollection(writer, descriptor.TargetScopes, WriteEndpointReference);
                writer.WriteEndElement();
            }

            WriteCustomElements(writer, descriptor);
        }

        protected virtual void WriteAttributeAttributes(XmlWriter writer, Saml2Attribute attribute)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            writer.WriteAttributeString("Name", attribute.Name);
            WriteUriAttributeIfPresent(writer, "NameFormat", null, attribute.NameFormat);
            WriteStringAttributeIfPresent(writer, "FriendlyName", null, attribute.FriendlyName);
            WriteCustomAttributes(writer, attribute);
        }

        protected virtual void WriteAttributeElements(XmlWriter writer,
            Saml2Attribute attribute)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            WriteCollection(writer, attribute.Values, (writer_, value) =>
                WriteStringElement(writer, "AttributeValue", Saml2AssertionNs, value));
            WriteCustomElements(writer, attribute);
        }

        protected virtual void WriteAttribute(XmlWriter writer, Saml2Attribute attribute)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            writer.WriteStartElement("Attribute", Saml2AssertionNs);
            WriteAttributeAttributes(writer, attribute);
            WriteAttributeElements(writer, attribute);
            writer.WriteEndElement();
        }
    }
}
