// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Text.Encodings.Web;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal static class HttpResponseBindings
{
    internal static string GenerateAutoPostForm(SamlMessageName messageName, string encodedMessage, Uri destination, string? relayState, bool includeCsp = false)
    {
        var relayStateField = relayState == null
            ? string.Empty
            : $@"<input type=""hidden"" name=""RelayState"" value=""{HtmlEncoder.Default.Encode(relayState)}"" />";

        var cspMetaTag = includeCsp
            ? $@"<meta http-equiv=""Content-Security-Policy"" content=""script-src '{IdentityServerConstants.ContentSecurityPolicyHashes.SamlAutoPostScript}'"" />"
            : string.Empty;

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    {cspMetaTag}
    <title>SAML Response</title>
</head>
<body>
    <noscript>
        <p><strong>Note:</strong> Since your browser does not support JavaScript, you must press the button below to proceed.</p>
    </noscript>
    <form method=""post"" action=""{HtmlEncoder.Default.Encode(destination.ToString())}"">
        <input type=""hidden"" name=""{messageName.Value}"" value=""{HtmlEncoder.Default.Encode(encodedMessage)}"" />
        {relayStateField}
        <noscript>
            <input type=""submit"" value=""Continue"" />
        </noscript>
    </form>
    <script>window.addEventListener('load', function () {{ document.forms[0].submit(); }});</script>
</body>
</html>";
    }
}
