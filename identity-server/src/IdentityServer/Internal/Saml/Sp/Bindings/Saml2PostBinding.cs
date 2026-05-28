// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Commands;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;
namespace Duende.IdentityServer.Internal.Saml.Sp.Bindings
{
    class Saml2PostBinding : Saml2Binding
    {
        protected internal override bool CanUnbind(HttpRequestData request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return request.HttpMethod == "POST"
                && (request.Form.Keys.Contains("SAMLResponse")
                    || request.Form.Keys.Contains("SAMLRequest"));
        }

        public override UnbindResult Unbind(HttpRequestData request, IOptions options)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!request.Form.TryGetValue("SAMLResponse", out string encodedMessage))
            {
                encodedMessage = request.Form["SAMLRequest"];
            }

            var xmlDoc = XmlHelpers.XmlDocumentFromString(
                Encoding.UTF8.GetString(
                    Convert.FromBase64String(encodedMessage)));

            options?.SPOptions.Logger.WriteVerbose("Http POST binding extracted message\n" + xmlDoc.OuterXml);

            request.Form.TryGetValue("RelayState", out string relayState);

            return new UnbindResult(xmlDoc.DocumentElement, relayState, TrustLevel.None);
        }

        public override CommandResult Bind(ISaml2Message message, ILoggerAdapter logger)
        {
            return Bind(message, logger, null);
        }

        public override CommandResult Bind<TMessage>(
            TMessage message, ILoggerAdapter logger, Action<TMessage, XDocument, Saml2BindingType> xmlCreatedNotification)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var xml = message.ToXml(xd => xmlCreatedNotification?.Invoke(message, xd, Saml2BindingType.HttpPost));

            if (message.SigningCertificate != null)
            {
                var xmlDoc = XmlHelpers.XmlDocumentFromString(xml);

                xmlDoc.Sign(message.SigningCertificate, true, message.SigningAlgorithm);
                xml = xmlDoc.OuterXml;
            }

            logger?.WriteVerbose("Sending message over Http POST binding\n" + xml);

            var encodedXml = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));

            var relayStateHtml = string.IsNullOrEmpty(message.RelayState) ? null
                : string.Format(CultureInfo.InvariantCulture, PostHtmlRelayStateFormatString, message.RelayState);

            var cr = new CommandResult()
            {
                ContentType = "text/html",
                Content = string.Format(
                    CultureInfo.InvariantCulture,
                    PostHtmlFormatString,
                    message.DestinationUrl,
                    relayStateHtml,
                    message.MessageName,
                    encodedXml)
            };

            return cr;
        }

        private const string PostHtmlRelayStateFormatString = @"
<input type=""hidden"" name=""RelayState"" value=""{0}""/>";

        private const string PostHtmlFormatString = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.1//EN""
""http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"" xml:lang=""en"">
<head>
<meta http-equiv=""Content-Security-Policy"" content=""script-src 'sha256-H3SVZBYrbqBt3ncrT/nNmOb6nwCjC12cPQzh5jnW4Y0='"">
</head>
<body>
<noscript>
<p>
<strong>Note:</strong> Since your browser does not support JavaScript, 
you must press the Continue button once to proceed.
</p>
</noscript>
<form action=""{0}"" method=""post"" name=""sustainsysSamlPostBindingSubmit"">
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
document.forms.sustainsysSamlPostBindingSubmit.submit();
</script>
</body>
</html>";
    }
}
