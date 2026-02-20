// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal class SamlErrorResponseXmlSerializer : ISamlResultSerializer<SamlErrorResponse>
{
    public XElement Serialize(SamlErrorResponse result)
    {
        var responseId = ResponseId.New().ToString();
        var issueInstant = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        var protocolNs = XNamespace.Get(SamlConstants.Namespaces.Protocol);
        var assertionNs = XNamespace.Get(SamlConstants.Namespaces.Assertion);

        // Build Status element
        var statusCodeElement = new XElement(protocolNs + "StatusCode",
            new XAttribute("Value", result.StatusCode.ToString()));

        // Add sub-status code if provided
        if (result.SubStatusCode?.Value != null)
        {
            statusCodeElement.Add(
                new XElement(protocolNs + "StatusCode",
                    new XAttribute("Value", result.SubStatusCode.Value.ToString())));
        }

        var statusElement = new XElement(protocolNs + "Status",
            statusCodeElement);

        // Add status message if provided
        if (!string.IsNullOrEmpty(result.Message))
        {
            statusElement.Add(
                new XElement(protocolNs + "StatusMessage", result.Message));
        }

        // Build Response element
        var responseElement = new XElement(protocolNs + "Response",
            new XAttribute("ID", responseId),
            new XAttribute("Version", "2.0"),
            new XAttribute("IssueInstant", issueInstant),
            new XAttribute("Destination", result.AssertionConsumerServiceUrl.ToString()),
            new XElement(assertionNs + "Issuer", result.Issuer.ToString()),
            statusElement);

        // Add InResponseTo if this is a response to a request
        if (result.InResponseTo != null)
        {
            responseElement.Add(new XAttribute("InResponseTo", result.InResponseTo));
        }

        return responseElement;
    }
}
