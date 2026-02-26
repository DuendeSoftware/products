// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

internal class SamlResponse : EndpointResult<SamlResponse>
{
    /// <summary>
    /// Gets or sets the unique identifier for this response.
    /// </summary>
    public string Id { get; } = SamlIds.NewResponseId();

    /// <summary>
    /// Gets or sets the SAML version. Must be "2.0".
    /// </summary>
    public string Version { get; } = SamlVersions.V2;

    /// <summary>
    /// Gets or sets the time instant of issue in UTC.
    /// </summary>
    public required DateTime IssueInstant { get; set; }

    /// <summary>
    /// Gets or sets the URI of the destination endpoint where this response is sent.
    /// This is the SP's Assertion Consumer Service (ACS) URL.
    /// </summary>
    public required Uri Destination { get; set; }

    /// <summary>
    /// Gets or sets the entity identifier of the Identity Provider sending this response.
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// Gets or sets the ID of the request to which this is a response.
    /// Will be null for IdP-initiated SSO (unsolicited responses).
    /// </summary>
    public string? InResponseTo { get; set; }

    /// <summary>
    /// Gets or sets the status of this response.
    /// </summary>
    public required Status Status { get; set; }

    /// <summary>
    /// Gets or sets the assertion included in this response.
    /// Will be null for error responses.
    /// </summary>
    public Assertion? Assertion { get; set; }

    /// <summary>
    /// Gets or sets the relay state included in this response.
    /// </summary>
    public string? RelayState { get; set; }

    /// <summary>
    /// Gets or sets the Service Provider where the response will be sent.
    /// </summary>
    public required SamlServiceProvider ServiceProvider { get; init; }

    internal class ResponseWriter(
        ISamlResultSerializer<SamlResponse> serializer,
        SamlResponseSigner samlResponseSigner,
        SamlAssertionEncryptor samlAssertionEncryptor) : IHttpResponseWriter<SamlResponse>
    {
        public async Task WriteHttpResponse(SamlResponse result, HttpContext httpContext)
        {
            var responseXml = serializer.Serialize(result);

            var signedResponseXml = await samlResponseSigner.SignResponse(responseXml, result.ServiceProvider, httpContext.RequestAborted);

            if (result.ServiceProvider.EncryptAssertions)
            {
                signedResponseXml = samlAssertionEncryptor.EncryptAssertion(signedResponseXml, result.ServiceProvider);
            }

            var encodedResponse = Convert.ToBase64String(Encoding.UTF8.GetBytes(signedResponseXml));

            var html = HttpResponseBindings.GenerateAutoPostForm(SamlConstants.RequestProperties.SAMLResponse, encodedResponse, result.Destination, result.RelayState);

            httpContext.Response.ContentType = "text/html";
            httpContext.Response.Headers.CacheControl = "no-cache, no-store";
            httpContext.Response.Headers.Pragma = "no-cache";

            await httpContext.Response.WriteAsync(html);
        }
    }

    internal class Serializer : ISamlResultSerializer<SamlResponse>
    {
        public XElement Serialize(SamlResponse toSerialize)
        {
            var issueInstant = toSerialize.IssueInstant.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            var protocolNs = XNamespace.Get(SamlConstants.Namespaces.Protocol);
            var assertionNs = XNamespace.Get(SamlConstants.Namespaces.Assertion);

            // Build Status element
            var statusCodeElement = new XElement(protocolNs + "StatusCode",
                new XAttribute("Value", toSerialize.Status.StatusCode.ToString()));

            if (!string.IsNullOrEmpty(toSerialize.Status.NestedStatusCode))
            {
                statusCodeElement.Add(
                    new XElement(protocolNs + "StatusCode",
                        new XAttribute("Value", toSerialize.Status.NestedStatusCode)));
            }

            var statusElement = new XElement(protocolNs + "Status", statusCodeElement);

            if (!string.IsNullOrEmpty(toSerialize.Status.StatusMessage))
            {
                statusElement.Add(new XElement(protocolNs + "StatusMessage", toSerialize.Status.StatusMessage));
            }

            // Build Response element
            var responseElement = new XElement(protocolNs + "Response",
                new XAttribute("ID", toSerialize.Id.ToString()),
                new XAttribute("Version", toSerialize.Version.ToString()),
                new XAttribute("IssueInstant", issueInstant),
                new XAttribute("Destination", toSerialize.Destination.ToString()),
                new XElement(assertionNs + "Issuer", toSerialize.Issuer.ToString()),
                statusElement);

            if (toSerialize.InResponseTo != null)
            {
                responseElement.Add(new XAttribute("InResponseTo", toSerialize.InResponseTo));
            }

            // Add Assertion if present
            if (toSerialize.Assertion != null)
            {
                responseElement.Add(GenerateAssertionElement(toSerialize.Assertion, assertionNs, protocolNs));
            }

            return responseElement;
        }


        private static XElement GenerateAssertionElement(Assertion assertion, XNamespace assertionNs, XNamespace protocolNs)
        {
            var assertionElement = new XElement(assertionNs + "Assertion",
                new XAttribute("ID", assertion.Id.ToString()),
                new XAttribute("Version", assertion.Version.ToString()),
                new XAttribute("IssueInstant", assertion.IssueInstant.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)),
                new XElement(assertionNs + "Issuer", assertion.Issuer.ToString()));

            // Add Subject
            if (assertion.Subject != null)
            {
                assertionElement.Add(GenerateSubjectElement(assertion.Subject, assertionNs));
            }

            // Add Conditions
            if (assertion.Conditions != null)
            {
                assertionElement.Add(GenerateConditionsElement(assertion.Conditions, assertionNs));
            }

            // Add AuthnStatements
            foreach (var authnStatement in assertion.AuthnStatements)
            {
                assertionElement.Add(GenerateAuthnStatementElement(authnStatement, assertionNs));
            }

            // Add AttributeStatements
            foreach (var attributeStatement in assertion.AttributeStatements)
            {
                assertionElement.Add(GenerateAttributeStatementElement(attributeStatement, assertionNs));
            }

            return assertionElement;
        }

        private static XElement GenerateSubjectElement(Subject subject, XNamespace assertionNs)
        {
            var subjectElement = new XElement(assertionNs + "Subject");

            if (subject.NameId != null)
            {
                var nameIdElement = new XElement(assertionNs + "NameID", subject.NameId.Value);

                if (!string.IsNullOrEmpty(subject.NameId.Format))
                {
                    nameIdElement.Add(new XAttribute("Format", subject.NameId.Format));
                }

                if (!string.IsNullOrEmpty(subject.NameId.NameQualifier))
                {
                    nameIdElement.Add(new XAttribute("NameQualifier", subject.NameId.NameQualifier));
                }

                if (subject.NameId.SPNameQualifier != null)
                {
                    nameIdElement.Add(new XAttribute("SPNameQualifier", subject.NameId.SPNameQualifier));
                }

                subjectElement.Add(nameIdElement);
            }

            foreach (var confirmation in subject.SubjectConfirmations)
            {
                var confirmationElement = new XElement(assertionNs + "SubjectConfirmation",
                    new XAttribute("Method", confirmation.Method));

                if (confirmation.Data != null)
                {
                    var dataElement = new XElement(assertionNs + "SubjectConfirmationData");

                    if (confirmation.Data.NotBefore.HasValue)
                    {
                        dataElement.Add(new XAttribute("NotBefore",
                            confirmation.Data.NotBefore.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
                    }

                    if (confirmation.Data.NotOnOrAfter.HasValue)
                    {
                        dataElement.Add(new XAttribute("NotOnOrAfter",
                            confirmation.Data.NotOnOrAfter.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
                    }

                    if (confirmation.Data.Recipient != null)
                    {
                        dataElement.Add(new XAttribute("Recipient", confirmation.Data.Recipient.ToString()));
                    }

                    if (confirmation.Data.InResponseTo != null)
                    {
                        dataElement.Add(new XAttribute("InResponseTo", confirmation.Data.InResponseTo));
                    }

                    confirmationElement.Add(dataElement);
                }

                subjectElement.Add(confirmationElement);
            }

            return subjectElement;
        }

        private static XElement GenerateConditionsElement(Conditions conditions, XNamespace assertionNs)
        {
            var conditionsElement = new XElement(assertionNs + "Conditions");

            if (conditions.NotBefore.HasValue)
            {
                conditionsElement.Add(new XAttribute("NotBefore",
                    conditions.NotBefore.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
            }

            if (conditions.NotOnOrAfter.HasValue)
            {
                conditionsElement.Add(new XAttribute("NotOnOrAfter",
                    conditions.NotOnOrAfter.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
            }

            if (conditions.AudienceRestrictions.Count > 0)
            {
                var audienceRestrictionElement = new XElement(assertionNs + "AudienceRestriction");
                foreach (var audience in conditions.AudienceRestrictions)
                {
                    audienceRestrictionElement.Add(new XElement(assertionNs + "Audience", audience));
                }
                conditionsElement.Add(audienceRestrictionElement);
            }

            return conditionsElement;
        }

        private static XElement GenerateAuthnStatementElement(AuthnStatement authnStatement, XNamespace assertionNs)
        {
            var authnStatementElement = new XElement(assertionNs + "AuthnStatement",
                new XAttribute("AuthnInstant", authnStatement.AuthnInstant.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));

            if (!string.IsNullOrEmpty(authnStatement.SessionIndex))
            {
                authnStatementElement.Add(new XAttribute("SessionIndex", authnStatement.SessionIndex));
            }

            if (authnStatement.SessionNotOnOrAfter.HasValue)
            {
                authnStatementElement.Add(new XAttribute("SessionNotOnOrAfter",
                    authnStatement.SessionNotOnOrAfter.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
            }

            if (authnStatement.AuthnContext != null && !string.IsNullOrEmpty(authnStatement.AuthnContext.AuthnContextClassRef))
            {
                var authnContextElement = new XElement(assertionNs + "AuthnContext",
                    new XElement(assertionNs + "AuthnContextClassRef", authnStatement.AuthnContext.AuthnContextClassRef));
                authnStatementElement.Add(authnContextElement);
            }

            return authnStatementElement;
        }

        private static XElement GenerateAttributeStatementElement(AttributeStatement attributeStatement, XNamespace assertionNs)
        {
            var attributeStatementElement = new XElement(assertionNs + "AttributeStatement");

            foreach (var attribute in attributeStatement.Attributes)
            {
                var attributeElement = new XElement(assertionNs + "Attribute",
                    new XAttribute("Name", attribute.Name));

                if (!string.IsNullOrEmpty(attribute.NameFormat))
                {
                    attributeElement.Add(new XAttribute("NameFormat", attribute.NameFormat));
                }

                if (!string.IsNullOrEmpty(attribute.FriendlyName))
                {
                    attributeElement.Add(new XAttribute("FriendlyName", attribute.FriendlyName));
                }

                foreach (var value in attribute.Values)
                {
                    attributeElement.Add(new XElement(assertionNs + "AttributeValue", value));
                }

                attributeStatementElement.Add(attributeElement);
            }

            return attributeStatementElement;
        }
    }
}
