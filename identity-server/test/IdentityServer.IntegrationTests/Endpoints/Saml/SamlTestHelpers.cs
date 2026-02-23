// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Web;
using System.Xml;
using Microsoft.Net.Http.Headers;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

internal static class SamlTestHelpers
{
    public static async Task<string> EncodeRequest(string authenticationRequest, CT ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(authenticationRequest);
        using var outputStream = new MemoryStream();
        await using (var deflateStream = new DeflateStream(outputStream, CompressionMode.Compress, leaveOpen: true))
        {
            await deflateStream.WriteAsync(bytes, 0, bytes.Length, ct);
        }

        var compressedBytes = outputStream.ToArray();
        var base64 = Convert.ToBase64String(compressedBytes);
        var urlEncoded = Uri.EscapeDataString(base64);
        return urlEncoded;
    }

    public static string ConvertToBase64Encoded(string authenticationRequest) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(authenticationRequest));

    /// <summary>
    /// Extracts SAML error response from an HTTP-POST binding auto-submit form.
    /// </summary>
    public static async Task<SamlErrorResponseData> ExtractSamlErrorFromPostAsync(HttpResponseMessage response, CT ct = default)
    {
        var (responseXml, relayState, acsUrl) = await ExtractSamlResponse(response, ct);
        var (samlpNs, samlNs, responseElement) = ParseSamlResponseXml(responseXml);
        var baseData = ParseCommonResponseElements(responseElement, samlNs, samlpNs, relayState, acsUrl);

        return new SamlErrorResponseData
        {
            ResponseId = baseData.ResponseId,
            InResponseTo = baseData.InResponseTo,
            Destination = baseData.Destination,
            IssueInstant = baseData.IssueInstant,
            Issuer = baseData.Issuer,
            StatusCode = baseData.StatusCode,
            StatusMessage = baseData.StatusMessage,
            SubStatusCode = baseData.SubStatusCode,
            RelayState = baseData.RelayState,
            AssertionConsumerServiceUrl = baseData.AssertionConsumerServiceUrl
        };
    }

    public static async Task<SamlLogoutResponseData> ExtractSamlLogoutResponseFromPostAsync(HttpResponseMessage response, CT ct = default)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (responseXml, relayState, acsUrl) = await ExtractSamlResponse(response, ct);
        var (samlpNs, samlNs, logoutResponseElement) = ParseSamlLogoutResponseXml(responseXml);
        var baseData = ParseCommonResponseElements(logoutResponseElement, samlNs, samlpNs, relayState, acsUrl);

        return new SamlLogoutResponseData
        {
            ResponseId = baseData.ResponseId,
            InResponseTo = baseData.InResponseTo,
            Destination = baseData.Destination,
            IssueInstant = baseData.IssueInstant,
            Issuer = baseData.Issuer,
            StatusCode = baseData.StatusCode,
            StatusMessage = baseData.StatusMessage,
            SubStatusCode = baseData.SubStatusCode,
            RelayState = baseData.RelayState,
            AssertionConsumerServiceUrl = baseData.AssertionConsumerServiceUrl
        };
    }

    public static async Task<SamlSuccessResponseData> ExtractSamlSuccessFromPostAsync(HttpResponseMessage response, CT ct = default)
    {
        var (responseXml, relayState, acsUrl) = await ExtractSamlResponse(response, ct);
        var (samlpNs, samlNs, responseElement) = ParseSamlResponseXml(responseXml);
        var baseData = ParseCommonResponseElements(responseElement, samlNs, samlpNs, relayState, acsUrl);

        var assertion = ParseAssertion(responseElement, samlNs);

        return new SamlSuccessResponseData
        {
            ResponseId = baseData.ResponseId,
            InResponseTo = baseData.InResponseTo,
            Destination = baseData.Destination,
            IssueInstant = baseData.IssueInstant,
            Issuer = baseData.Issuer,
            StatusCode = baseData.StatusCode,
            StatusMessage = baseData.StatusMessage,
            SubStatusCode = baseData.SubStatusCode,
            RelayState = baseData.RelayState,
            AssertionConsumerServiceUrl = baseData.AssertionConsumerServiceUrl,
            Assertion = assertion
        };
    }

    public static async Task<(string responseXml, string? relayState, string acsUrl)> ExtractSamlResponse(HttpResponseMessage response, CT ct = default)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");

        var html = await response.Content.ReadAsStringAsync(ct);

        // Extract SAMLResponse from hidden input field
        var samlResponseMatch = System.Text.RegularExpressions.Regex.Match(
            html,
            @"<input[^>]+name=""SAMLResponse""[^>]+value=""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        samlResponseMatch.Success.ShouldBeTrue("SAMLResponse input field not found in HTML");
        var encodedResponse = samlResponseMatch.Groups[1].Value;

        // Extract RelayState if present
        string? relayState = null;
        var relayStateMatch = System.Text.RegularExpressions.Regex.Match(
            html,
            @"<input[^>]+name=""RelayState""[^>]+value=""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (relayStateMatch.Success)
        {
            relayState = HttpUtility.HtmlDecode(relayStateMatch.Groups[1].Value);
        }

        // Extract form action (ACS URL)
        var actionMatch = System.Text.RegularExpressions.Regex.Match(
            html,
            @"<form[^>]+action=""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        actionMatch.Success.ShouldBeTrue("Form action not found in HTML");
        var acsUrl = HttpUtility.HtmlDecode(actionMatch.Groups[1].Value);

        // Decode the SAML response
        var decodedBytes = Convert.FromBase64String(HttpUtility.HtmlDecode(encodedResponse));
        var responseXml = Encoding.UTF8.GetString(decodedBytes);

        return (responseXml, relayState, acsUrl);
    }

    public static (System.Xml.Linq.XNamespace samlpNs, System.Xml.Linq.XNamespace samlNs, System.Xml.Linq.XElement responseElement) ParseSamlResponseXml(string responseXml)
    {
        var doc = System.Xml.Linq.XDocument.Parse(responseXml);
        var samlpNs = System.Xml.Linq.XNamespace.Get("urn:oasis:names:tc:SAML:2.0:protocol");
        var samlNs = System.Xml.Linq.XNamespace.Get("urn:oasis:names:tc:SAML:2.0:assertion");

        var responseElement = doc.Root;
        responseElement.ShouldNotBeNull();
        responseElement.Name.ShouldBe(samlpNs + "Response");

        return (samlpNs, samlNs, responseElement);
    }

    public static (System.Xml.Linq.XNamespace samlpNs, System.Xml.Linq.XNamespace samlNs, System.Xml.Linq.XElement logoutResponseElement) ParseSamlLogoutResponseXml(string responseXml)
    {
        var doc = System.Xml.Linq.XDocument.Parse(responseXml);
        var samlpNs = System.Xml.Linq.XNamespace.Get("urn:oasis:names:tc:SAML:2.0:protocol");
        var samlNs = System.Xml.Linq.XNamespace.Get("urn:oasis:names:tc:SAML:2.0:assertion");

        var logoutResponseElement = doc.Root;
        logoutResponseElement.ShouldNotBeNull();
        logoutResponseElement.Name.ShouldBe(samlpNs + "LogoutResponse");

        return (samlpNs, samlNs, logoutResponseElement);
    }

    public static SamlResponseBase ParseCommonResponseElements(
        System.Xml.Linq.XElement responseElement,
        System.Xml.Linq.XNamespace samlNs,
        System.Xml.Linq.XNamespace samlpNs,
        string? relayState,
        string acsUrl)
    {
        var responseId = responseElement.Attribute("ID")?.Value;
        var inResponseTo = responseElement.Attribute("InResponseTo")?.Value;
        var destination = responseElement.Attribute("Destination")?.Value;
        var issueInstant = responseElement.Attribute("IssueInstant")?.Value;
        var issuer = responseElement.Element(samlNs + "Issuer")?.Value;

        var statusElement = responseElement.Element(samlpNs + "Status");
        statusElement.ShouldNotBeNull();

        var statusCodeElement = statusElement.Element(samlpNs + "StatusCode");
        statusCodeElement.ShouldNotBeNull();
        var statusCode = statusCodeElement.Attribute("Value")?.Value;
        var statusMessage = statusElement.Element(samlpNs + "StatusMessage")?.Value;

        var subStatusCodeElement = statusCodeElement.Element(samlpNs + "StatusCode");
        var subStatusCode = subStatusCodeElement?.Attribute("Value")?.Value;

        return new SamlResponseBase
        {
            ResponseId = responseId,
            InResponseTo = inResponseTo,
            Destination = destination,
            IssueInstant = issueInstant,
            Issuer = issuer,
            StatusCode = statusCode,
            StatusMessage = statusMessage,
            SubStatusCode = subStatusCode,
            RelayState = relayState,
            AssertionConsumerServiceUrl = acsUrl
        };
    }

    public static Assertion ParseAssertion(System.Xml.Linq.XElement responseElement, System.Xml.Linq.XNamespace samlNs)
    {
        var assertionElement = responseElement.Element(samlNs + "Assertion");
        assertionElement.ShouldNotBeNull();

        var assertionId = assertionElement.Attribute("ID")?.Value;
        var assertionVersion = assertionElement.Attribute("Version")?.Value;
        var assertionIssueInstant = assertionElement.Attribute("IssueInstant")?.Value;
        var assertionIssuer = assertionElement.Element(samlNs + "Issuer")?.Value;

        var subjectElement = assertionElement.Element(samlNs + "Subject");
        Subject? subject = null;
        if (subjectElement != null)
        {
            var nameIdElement = subjectElement.Element(samlNs + "NameID");
            var subjectConfirmationElement = subjectElement.Element(samlNs + "SubjectConfirmation");

            SubjectConfirmation? subjectConfirmation = null;
            if (subjectConfirmationElement != null)
            {
                var subjectConfirmationDataElement = subjectConfirmationElement.Element(samlNs + "SubjectConfirmationData");
                SubjectConfirmationData? subjectConfirmationData = null;
                if (subjectConfirmationDataElement != null)
                {
                    subjectConfirmationData = new SubjectConfirmationData
                    {
                        NotOnOrAfter = subjectConfirmationDataElement.Attribute("NotOnOrAfter")?.Value,
                        Recipient = subjectConfirmationDataElement.Attribute("Recipient")?.Value,
                        InResponseTo = subjectConfirmationDataElement.Attribute("InResponseTo")?.Value
                    };
                }

                subjectConfirmation = new SubjectConfirmation
                {
                    Method = subjectConfirmationElement.Attribute("Method")?.Value,
                    SubjectConfirmationData = subjectConfirmationData
                };
            }

            subject = new Subject
            {
                NameId = nameIdElement?.Value,
                NameIdFormat = nameIdElement?.Attribute("Format")?.Value,
                SPNameQualifier = nameIdElement?.Attribute("SPNameQualifier")?.Value,
                SubjectConfirmation = subjectConfirmation
            };
        }

        var conditionsElement = assertionElement.Element(samlNs + "Conditions");
        Conditions? conditions = null;
        if (conditionsElement != null)
        {
            var audienceRestrictionElement = conditionsElement.Element(samlNs + "AudienceRestriction");
            var audienceElement = audienceRestrictionElement?.Element(samlNs + "Audience");

            conditions = new Conditions
            {
                NotBefore = conditionsElement.Attribute("NotBefore")?.Value,
                NotOnOrAfter = conditionsElement.Attribute("NotOnOrAfter")?.Value,
                Audience = audienceElement?.Value
            };
        }

        var authnStatementElement = assertionElement.Element(samlNs + "AuthnStatement");
        AuthnStatement? authnStatement = null;
        if (authnStatementElement != null)
        {
            var authnContextElement = authnStatementElement.Element(samlNs + "AuthnContext");
            var authnContextClassRefElement = authnContextElement?.Element(samlNs + "AuthnContextClassRef");

            authnStatement = new AuthnStatement
            {
                AuthnInstant = authnStatementElement.Attribute("AuthnInstant")?.Value,
                SessionIndex = authnStatementElement.Attribute("SessionIndex")?.Value,
                AuthnContextClassRef = authnContextClassRefElement?.Value
            };
        }

        var attributeStatementElement = assertionElement.Element(samlNs + "AttributeStatement");
        List<SamlAttribute>? attributes = null;
        if (attributeStatementElement != null)
        {
            attributes = attributeStatementElement.Elements(samlNs + "Attribute")
                .Select(attr =>
                {
                    var attributeValues = attr.Elements(samlNs + "AttributeValue")
                        .Select(av => av.Value)
                        .ToList();

                    return new SamlAttribute
                    {
                        Name = attr.Attribute("Name")?.Value,
                        NameFormat = attr.Attribute("NameFormat")?.Value,
                        FriendlyName = attr.Attribute("FriendlyName")?.Value,
                        Value = attributeValues.FirstOrDefault(), // For backward compatibility
                        Values = attributeValues
                    };
                })
                .ToList();
        }

        return new Assertion
        {
            Id = assertionId,
            Version = assertionVersion,
            IssueInstant = assertionIssueInstant,
            Issuer = assertionIssuer,
            Subject = subject,
            Conditions = conditions,
            AuthnStatement = authnStatement,
            Attributes = attributes
        };
    }

    public record SamlResponseBase
    {
        public string? ResponseId { get; init; }
        public string? InResponseTo { get; init; }
        public string? Destination { get; init; }
        public string? IssueInstant { get; init; }
        public string? Issuer { get; init; }
        public string? StatusCode { get; init; }
        public string? StatusMessage { get; init; }
        public string? SubStatusCode { get; init; }
        public string? RelayState { get; init; }
        public string? AssertionConsumerServiceUrl { get; init; }
    }

    public record SamlErrorResponseData : SamlResponseBase
    {
    }

    public record SamlSuccessResponseData : SamlResponseBase
    {
        public required Assertion Assertion { get; init; }
    }

    public record Assertion
    {
        public string? Id { get; init; }
        public string? Version { get; init; }
        public string? IssueInstant { get; init; }
        public string? Issuer { get; init; }
        public Subject? Subject { get; init; }
        public Conditions? Conditions { get; init; }
        public AuthnStatement? AuthnStatement { get; init; }
        public List<SamlAttribute>? Attributes { get; init; }
    }

    public record Subject
    {
        public string? NameId { get; init; }
        public string? NameIdFormat { get; init; }
        public string? SPNameQualifier { get; init; }
        public SubjectConfirmation? SubjectConfirmation { get; init; }
    }

    public record SubjectConfirmation
    {
        public string? Method { get; init; }
        public SubjectConfirmationData? SubjectConfirmationData { get; init; }
    }

    public record SubjectConfirmationData
    {
        public string? NotOnOrAfter { get; init; }
        public string? Recipient { get; init; }
        public string? InResponseTo { get; init; }
    }

    public record Conditions
    {
        public string? NotBefore { get; init; }
        public string? NotOnOrAfter { get; init; }
        public string? Audience { get; init; }
    }

    public record AuthnStatement
    {
        public string? AuthnInstant { get; init; }
        public string? SessionIndex { get; init; }
        public string? AuthnContextClassRef { get; init; }
    }

    public record SamlAttribute
    {
        public string? Name { get; init; }
        public string? NameFormat { get; init; }
        public string? FriendlyName { get; init; }
        public string? Value { get; init; }
        public List<string> Values { get; init; } = new();
    }

    public static void VerifySignaturePresence(string responseXml, bool expectResponseSignature, bool expectAssertionSignature)
    {
        var hasResponseSig = HasResponseSignature(responseXml);
        var hasAssertionSig = HasAssertionSignature(responseXml);

        if (expectResponseSignature)
        {
            hasResponseSig.ShouldBeTrue("Expected Response to have a Signature element");
        }
        else
        {
            hasResponseSig.ShouldBeFalse("Expected Response to NOT have a Signature element");
        }

        if (expectAssertionSignature)
        {
            hasAssertionSig.ShouldBeTrue("Expected Assertion to have a Signature element");
        }
        else
        {
            hasAssertionSig.ShouldBeFalse("Expected Assertion to NOT have a Signature element");
        }
    }

    public static void VerifySignaturePositionAfterIssuer(System.Xml.Linq.XElement parentElement)
    {
        var samlNs = System.Xml.Linq.XNamespace.Get("urn:oasis:names:tc:SAML:2.0:assertion");
        var dsNs = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");

        var issuerElement = parentElement.Element(samlNs + "Issuer");
        var signatureElement = parentElement.Element(dsNs + "Signature");

        issuerElement.ShouldNotBeNull("Parent element must have an Issuer");
        signatureElement.ShouldNotBeNull("Parent element must have a Signature");

        // Check that Signature comes after Issuer in document order
        var elements = parentElement.Elements().ToList();
        var issuerIndex = elements.IndexOf(issuerElement!);
        var signatureIndex = elements.IndexOf(signatureElement!);

        signatureIndex.ShouldBeGreaterThan(issuerIndex,
            "Signature element must appear after Issuer element per SAML specification");
    }

    public static System.Xml.Linq.XElement? ExtractSignatureElement(System.Xml.Linq.XElement parentElement)
    {
        var dsNs = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");
        return parentElement.Element(dsNs + "Signature");
    }

    public static string? GetSignatureReferenceUri(System.Xml.Linq.XElement signatureElement)
    {
        var dsNs = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");
        var referenceElement = signatureElement
            .Element(dsNs + "SignedInfo")
            ?.Element(dsNs + "Reference");

        return referenceElement?.Attribute("URI")?.Value;
    }

    public static SignatureInfo ParseSignatureInfo(System.Xml.Linq.XElement signatureElement)
    {
        var dsNs = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");

        var signedInfo = signatureElement.Element(dsNs + "SignedInfo");
        signedInfo.ShouldNotBeNull("Signature must have SignedInfo element");

        var canonicalizationMethod = signedInfo!
            .Element(dsNs + "CanonicalizationMethod")
            ?.Attribute("Algorithm")?.Value;

        var signatureMethod = signedInfo
            .Element(dsNs + "SignatureMethod")
            ?.Attribute("Algorithm")?.Value;

        var reference = signedInfo.Element(dsNs + "Reference");
        var referenceUri = reference?.Attribute("URI")?.Value;

        var digestMethod = reference?
            .Element(dsNs + "DigestMethod")
            ?.Attribute("Algorithm")?.Value;

        return new SignatureInfo
        {
            CanonicalizationMethod = canonicalizationMethod,
            SignatureMethod = signatureMethod,
            ReferenceUri = referenceUri,
            DigestMethod = digestMethod
        };
    }

    private static bool HasResponseSignature(string responseXml)
    {
        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);
        var dsNs = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");
        var signatureElement = responseElement.Element(dsNs + "Signature");
        return signatureElement != null;
    }

    private static bool HasAssertionSignature(string responseXml)
    {
        var (_, samlNs, responseElement) = ParseSamlResponseXml(responseXml);
        var dsNs = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");

        var assertionElement = responseElement.Element(samlNs + "Assertion");
        if (assertionElement == null)
        {
            return false;
        }

        var signatureElement = assertionElement.Element(dsNs + "Signature");
        return signatureElement != null;
    }

    public record SignatureInfo
    {
        public string? CanonicalizationMethod { get; init; }
        public string? SignatureMethod { get; init; }
        public string? ReferenceUri { get; init; }
        public string? DigestMethod { get; init; }
    }

    public static string? ExtractStateIdFromCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            return null;
        }

        if (!CookieHeaderValue.TryParseList(setCookies.ToList(), out var cookieHeaderValues))
        {
            return null;
        }

        var targetCookie = cookieHeaderValues.FirstOrDefault(cookie => cookie.Name == "__IdsSvr_SamlSigninState");

        return targetCookie?.Value.ToString();
    }

    public static X509Certificate2 CreateTestSigningCertificate(TimeProvider timeProvider, string subject = "CN=Test SP Signing Certificate")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

        var certificate = request.CreateSelfSigned(
            timeProvider.GetUtcNow().AddDays(-1),
            timeProvider.GetUtcNow().AddYears(10));

        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    public static string SignAuthNRequestXml(string authNRequestXml, X509Certificate2 certificate)
    {
        var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        doc.LoadXml(authNRequestXml);

        var signedXml = new SignedXml(doc) { SigningKey = certificate.GetRSAPrivateKey() };

        var reference = new Reference { Uri = "#" + doc.DocumentElement!.GetAttribute("ID") };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(reference);

        signedXml.KeyInfo = new KeyInfo();
        signedXml.KeyInfo.AddClause(new KeyInfoX509Data(certificate));

        signedXml.ComputeSignature();

        var signatureElement = signedXml.GetXml();

        // Insert signature after Issuer element per SAML spec
        var issuerElement = doc.DocumentElement!.GetElementsByTagName("Issuer", "urn:oasis:names:tc:SAML:2.0:assertion")[0];
        doc.DocumentElement.InsertAfter(signatureElement, issuerElement);

        return doc.OuterXml;
    }

    public static (string signature, string sigAlg) SignAuthNRequestRedirect(
        string samlRequest,
        string? relayState,
        X509Certificate2 certificate,
        string algorithm = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256")
    {
        // Build the query string to sign (order matters!)
        var queryToSign = $"SAMLRequest={samlRequest}";

        if (!string.IsNullOrEmpty(relayState))
        {
            queryToSign += $"&RelayState={Uri.EscapeDataString(relayState)}";
        }

        queryToSign += $"&SigAlg={Uri.EscapeDataString(algorithm)}";

        var bytesToSign = Encoding.UTF8.GetBytes(queryToSign);

        using var rsa = certificate.GetRSAPrivateKey();
        var hashAlgorithm = algorithm.Contains("sha512") ? HashAlgorithmName.SHA512 : HashAlgorithmName.SHA256;
        var signatureBytes = rsa!.SignData(bytesToSign, hashAlgorithm, RSASignaturePadding.Pkcs1);

        var signature = Convert.ToBase64String(signatureBytes);

        return (signature, algorithm);
    }

    public static X509Certificate2 CreateExpiredTestSigningCertificate(TimeProvider timeProvider, string subject = "CN=Expired Test SP Certificate")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

        // Create certificate that expired yesterday
        var certificate = request.CreateSelfSigned(
            timeProvider.GetUtcNow().AddYears(-2),
            timeProvider.GetUtcNow().AddDays(-1));

        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    public static X509Certificate2 CreateNotYetValidTestSigningCertificate(TimeProvider timeProvider, string subject = "CN=Future Test SP Certificate")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

        // Create certificate that won't be valid until tomorrow
        var certificate = request.CreateSelfSigned(
            timeProvider.GetUtcNow().AddDays(1),
            timeProvider.GetUtcNow().AddYears(10));

        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    public static string SignAuthNRequestXmlWithEmptyReference(string authNRequestXml, X509Certificate2 certificate)
    {
        var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        doc.LoadXml(authNRequestXml);

        var signedXml = new SignedXml(doc) { SigningKey = certificate.GetRSAPrivateKey() };

        // Create reference with empty URI - this should fail validation
        var reference = new Reference { Uri = "" };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(reference);

        signedXml.KeyInfo = new KeyInfo();
        signedXml.KeyInfo.AddClause(new KeyInfoX509Data(certificate));

        signedXml.ComputeSignature();

        var signatureElement = signedXml.GetXml();

        // Insert signature after Issuer element per SAML spec
        var issuerElement = doc.DocumentElement!.GetElementsByTagName("Issuer", "urn:oasis:names:tc:SAML:2.0:assertion")[0];
        doc.DocumentElement.InsertAfter(signatureElement, issuerElement);

        return doc.OuterXml;
    }

    public static void ValidateEncryptedStructure(System.Xml.Linq.XElement response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var samlNs = System.Xml.Linq.XNamespace.Get(
            "urn:oasis:names:tc:SAML:2.0:assertion");
        var encNs = System.Xml.Linq.XNamespace.Get(
            "http://www.w3.org/2001/04/xmlenc#");

        // Verify <EncryptedAssertion> present
        var encAssertion = response.Descendants(samlNs + "EncryptedAssertion")
            .FirstOrDefault();
        encAssertion.ShouldNotBeNull(
            "Response should contain <EncryptedAssertion> element");

        // Verify <EncryptedData> present
        var encData = encAssertion.Descendants(encNs + "EncryptedData")
            .FirstOrDefault();
        encData.ShouldNotBeNull(
            "<EncryptedAssertion> should contain <EncryptedData> element");

        // Verify <EncryptedKey> present
        var encKey = encAssertion.Descendants(encNs + "EncryptedKey")
            .FirstOrDefault();
        encKey.ShouldNotBeNull(
            "<EncryptedAssertion> should contain <EncryptedKey> element");
    }

    public static bool HasEncryptedAssertion(string responseXml)
    {
        var (_, samlNs, responseElement) = ParseSamlResponseXml(responseXml);
        var encryptedAssertion = responseElement.Element(samlNs + "EncryptedAssertion");
        return encryptedAssertion != null;
    }

    public static bool HasPlainAssertion(string responseXml)
    {
        var (_, samlNs, responseElement) = ParseSamlResponseXml(responseXml);
        var assertion = responseElement.Element(samlNs + "Assertion");
        return assertion != null;
    }

    public static System.Xml.Linq.XElement DecryptAssertion(
        System.Xml.Linq.XElement encryptedAssertion,
        X509Certificate2 decryptionCertificate)
    {
        ArgumentNullException.ThrowIfNull(encryptedAssertion);
        ArgumentNullException.ThrowIfNull(decryptionCertificate);

        using var privateKey = decryptionCertificate.GetRSAPrivateKey();
        if (privateKey == null)
        {
            throw new CryptographicException("Certificate does not contain an RSA private key");
        }

        // Convert to XmlDocument for EncryptedXml API
        var xmlDoc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using (var reader = encryptedAssertion.CreateReader())
        {
            xmlDoc.Load(reader);
        }

        var nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
        nsManager.AddNamespace("xenc", "http://www.w3.org/2001/04/xmlenc#");

        var encryptedDataElement = xmlDoc.SelectSingleNode("//xenc:EncryptedData", nsManager) as XmlElement;
        if (encryptedDataElement == null)
        {
            throw new InvalidOperationException("No EncryptedData element found");
        }

        var encryptedData = new EncryptedData();
        encryptedData.LoadXml(encryptedDataElement);

        // The encryption was done with EncryptedXml.Encrypt(element, certificate)
        // which embeds an EncryptedKey with the certificate info
        // We need to decrypt the key first, then decrypt the data

        // Find and decrypt the EncryptedKey
        var encryptedKeyElement = xmlDoc.SelectSingleNode("//xenc:EncryptedKey", nsManager) as XmlElement;
        if (encryptedKeyElement == null)
        {
            throw new InvalidOperationException("No EncryptedKey element found");
        }

        var encryptedKey = new EncryptedKey();
        encryptedKey.LoadXml(encryptedKeyElement);
        if (encryptedKey.CipherData.CipherValue == null)
        {
            throw new InvalidOperationException("No CipherValue found in encrypted key element");
        }

        // Decrypt the session key using our RSA private key
        byte[] sessionKey;

        // The Encrypt method uses RSA-OAEP by default
        if (encryptedKey.EncryptionMethod?.KeyAlgorithm == EncryptedXml.XmlEncRSAOAEPUrl)
        {
            sessionKey = privateKey.Decrypt(encryptedKey.CipherData.CipherValue, RSAEncryptionPadding.OaepSHA1);
        }
        else if (encryptedKey.EncryptionMethod?.KeyAlgorithm == EncryptedXml.XmlEncRSA15Url)
        {
            sessionKey = privateKey.Decrypt(encryptedKey.CipherData.CipherValue, RSAEncryptionPadding.Pkcs1);
        }
        else
        {
            throw new CryptographicException($"Unsupported key encryption algorithm: {encryptedKey.EncryptionMethod?.KeyAlgorithm}");
        }

        // Now decrypt the data using the session key
        var encryptedXml = new EncryptedXml();
        byte[] decryptedBytes;

        // Determine the symmetric algorithm used
        var algorithm = encryptedData.EncryptionMethod?.KeyAlgorithm;
        if (string.IsNullOrEmpty(algorithm))
        {
            throw new CryptographicException("No encryption algorithm specified");
        }

        // Create the appropriate symmetric algorithm
        SymmetricAlgorithm? symmetricAlgorithm = algorithm switch
        {
            EncryptedXml.XmlEncAES256Url => Aes.Create(),
            EncryptedXml.XmlEncAES192Url => Aes.Create(),
            EncryptedXml.XmlEncAES128Url => Aes.Create(),
            EncryptedXml.XmlEncTripleDESUrl => TripleDES.Create(),
            _ => throw new CryptographicException($"Unsupported encryption algorithm: {algorithm}")
        };

        if (symmetricAlgorithm == null)
        {
            throw new CryptographicException("Failed to create symmetric algorithm");
        }

        using (symmetricAlgorithm)
        {
            symmetricAlgorithm.Key = sessionKey;

            // Decrypt the data
            decryptedBytes = encryptedXml.DecryptData(encryptedData, symmetricAlgorithm);
        }

        // Convert to string and parse
        var decryptedXml = System.Text.Encoding.UTF8.GetString(decryptedBytes);
        return System.Xml.Linq.XElement.Parse(decryptedXml);
    }

    public static async Task<SamlSuccessResponseData> ExtractAndDecryptSamlSuccessFromPostAsync(
        HttpResponseMessage response,
        X509Certificate2 decryptionCertificate,
        CT ct = default)
    {
        var (responseXml, relayState, acsUrl) = await ExtractSamlResponse(response, ct);
        var (samlpNs, samlNs, responseElement) = ParseSamlResponseXml(responseXml);
        var baseData = ParseCommonResponseElements(responseElement, samlNs, samlpNs, relayState, acsUrl);

        // Get the EncryptedAssertion element
        var encryptedAssertion = responseElement.Element(samlNs + "EncryptedAssertion");
        if (encryptedAssertion == null)
        {
            throw new InvalidOperationException("Response does not contain an EncryptedAssertion element");
        }

        // Decrypt it - this returns the Assertion element
        var decryptedAssertion = DecryptAssertion(encryptedAssertion, decryptionCertificate);

        // Create a temporary container to hold the decrypted assertion for parsing
        var tempResponse = new System.Xml.Linq.XElement(samlpNs + "Response",
            new System.Xml.Linq.XAttribute("ID", "_temp"),
            new System.Xml.Linq.XAttribute("Version", "2.0"),
            decryptedAssertion);

        // Parse the decrypted assertion
        var assertion = ParseAssertion(tempResponse, samlNs);

        return new SamlSuccessResponseData
        {
            ResponseId = baseData.ResponseId,
            InResponseTo = baseData.InResponseTo,
            Destination = baseData.Destination,
            IssueInstant = baseData.IssueInstant,
            Issuer = baseData.Issuer,
            StatusCode = baseData.StatusCode,
            StatusMessage = baseData.StatusMessage,
            SubStatusCode = baseData.SubStatusCode,
            RelayState = baseData.RelayState,
            AssertionConsumerServiceUrl = baseData.AssertionConsumerServiceUrl,
            Assertion = assertion
        };
    }

    public record SamlLogoutResponseData : SamlResponseBase
    {
    }
}
