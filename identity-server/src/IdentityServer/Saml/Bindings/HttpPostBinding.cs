// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Globalization;
using System.Net;
using System.Text;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Saml.Infrastructure;
using Duende.IdentityServer.Saml.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Bindings;

/// <summary>
/// Saml Http POST Binding
/// </summary>
public class HttpPostBinding : FrontChannelBinding
{
    private readonly IdentityServerOptions _options;

    /// <summary>
    /// Constructor
    /// </summary>
    public HttpPostBinding(IOptions<IdentityServerOptions> options) : base(SamlConstants.Bindings.HttpPost)
        => _options = options.Value;

    /// <inheritdoc/>
    public override bool CanUnBind(HttpRequest httpRequest)
        => httpRequest.Method == "POST"
        && httpRequest.Form.Keys.Any(
            k => k == SamlConstants.RequestProperties.SAMLRequest || k == SamlConstants.RequestProperties.SAMLResponse);

    /// <inheritdoc/>
    protected override Task<InboundSaml2Message> DoUnBindAsync(
        HttpRequest httpRequest,
        Func<string, Ct, Task<Saml2Entity?>> entityResolver)
    {
        if (!CanUnBind(httpRequest))
        {
            throw new InvalidOperationException("Cannot unbind from this request. Always call CanUnbind before UnbindAsync to validate.");
        }

        string name;

        if (httpRequest.Form.ContainsKey(SamlConstants.RequestProperties.SAMLRequest))
        {
            if (httpRequest.Form.ContainsKey(SamlConstants.RequestProperties.SAMLResponse))
            {
                throw new ArgumentException("Either SamlResponse or SamlRequest should be defined, not both.");
            }
            name = SamlConstants.RequestProperties.SAMLRequest;
        }
        else
        {
            if (httpRequest.Form.ContainsKey(SamlConstants.RequestProperties.SAMLResponse))
            {
                name = SamlConstants.RequestProperties.SAMLResponse;
            }
            else
            {
                // No need to handle case where none was present - CanUnbind should have taken care of that.
                throw new NotImplementedException();
            }
        }

        var encoded = httpRequest.Form[name].Single()
            ?? throw new InvalidOperationException("No form content found");

        // Base64 decodes to ~75% of the encoded length. Reject early to avoid
        // allocating a large decoded string before SecureXmlParser's own check.
        if (encoded.Length > _options.Saml.MaxMessageSize * 4 / 3)
        {
            throw new InvalidOperationException(
                $"SAML message exceeds maximum allowed size of {_options.Saml.MaxMessageSize} characters.");
        }

        var xml = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        var xd = SecureXmlParser.LoadXmlDocument(xml, _options.Saml.MaxMessageSize);

        var relayState = httpRequest.Form[SamlConstants.RequestProperties.RelayState].SingleOrDefault();
        if (relayState != null && Encoding.UTF8.GetByteCount(relayState) > _options.Saml.MaxRelayStateLength)
        {
            throw new InvalidOperationException(
                $"RelayState exceeds maximum allowed size of {_options.Saml.MaxRelayStateLength} bytes.");
        }

        return Task.FromResult(new InboundSaml2Message
        {
            Destination = httpRequest.PathBase + httpRequest.Path,
            Name = name,
            RelayState = relayState,
            Xml = xd.DocumentElement!,
            Binding = Identifier
        });
    }

    /// <inheritdoc/>
    protected override async Task DoBindAsync(
        HttpResponse httpResponse,
        OutboundSaml2Message message)
    {
        SignMessage(message);
        var content = BuildAutoPostHtml(message);
        httpResponse.ContentType = "text/html";
        httpResponse.Headers["Content-Security-Policy"] = $"script-src '{IdentityServerConstants.ContentSecurityPolicyHashes.SamlAutoPostScript}'";
        httpResponse.Headers.CacheControl = "no-cache, no-store";
        await httpResponse.WriteAsync(content);
    }

    /// <summary>
    /// Signs the root element of the SAML message.
    /// </summary>
    /// <param name="message">The SAML message containing the XML and signing configuration.</param>
    protected virtual void SignMessage(OutboundSaml2Message message)
    {
        if (message.SigningCertificate == null)
        {
            return;
        }

        var responseElement = message.Xml;
        var issuerElement = responseElement["Issuer", SamlConstants.Namespaces.Assertion]
            ?? throw new InvalidOperationException("Root element must contain an Issuer element.");
        responseElement.Sign(message.SigningCertificate, issuerElement);
    }

    /// <summary>
    /// Builds the HTML auto-POST form string for the given SAML message.
    /// <see cref="Saml2Message.Name"/>, <see cref="Saml2Message.RelayState"/>, and
    /// <see cref="Saml2Message.Destination"/> are HTML-encoded before being placed into
    /// HTML attributes to prevent XSS.
    /// </summary>
    private static string BuildAutoPostHtml(OutboundSaml2Message message)
    {
        var relayStateHtml = string.IsNullOrEmpty(message.RelayState) ? null
            : string.Format(
                CultureInfo.InvariantCulture,
                PostHtmlRelayStateFormat,
                WebUtility.HtmlEncode(message.RelayState));

        var encodedXml = Convert.ToBase64String(Encoding.UTF8.GetBytes(message.Xml.OuterXml));

        return string.Format(
            CultureInfo.InvariantCulture,
            PostHtmlFormat,
            WebUtility.HtmlEncode(message.Destination),
            relayStateHtml,
            WebUtility.HtmlEncode(message.Name),
            encodedXml);
    }

    private const string PostHtmlRelayStateFormatString = @"
<input type=""hidden"" name=""RelayState"" value=""{0}""/>";

    private static readonly CompositeFormat PostHtmlRelayStateFormat = CompositeFormat.Parse(PostHtmlRelayStateFormatString);

    private const string PostHtmlFormatString = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.1//EN""
""http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"" xml:lang=""en"">
<head/>
<body>
<noscript>
<p>
<strong>Note:</strong> Since your browser does not support JavaScript,
you must press the Continue button once to proceed.
</p>
</noscript>
<form action=""{0}"" method=""post"" name=""duendeSamlPostBindingSubmit"">
<div>{1}
<input type=""hidden"" name=""{2}""
value=""{3}""/>
</div>
<noscript>
<div>
<input type=""submit"" value=""Continue""/>
</div>
</noscript>
</form>
<script type=""text/javascript"">
document.forms.duendeSamlPostBindingSubmit.submit();
</script>
</body>
</html>";

    private static readonly CompositeFormat PostHtmlFormat = CompositeFormat.Parse(PostHtmlFormatString);

}
