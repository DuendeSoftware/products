// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Duende.IdentityServer.Internal.Saml.Sp.Bindings;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;

namespace UnitTests.Saml.Sp;

public sealed class Saml2PostBindingTests
{
    private const string ExpectedScriptHash = "sha256-IQKtK10TFgRroV/L1+sRadhw5yAEkHE3GlbgJgxr7K4=";

    [Fact]
    public void emitted_script_hash_matches_emitted_CSP_hash()
    {
        var result = Bind(new TestSaml2Message());
        var script = Regex.Match(
            result.Content,
            "<script type=\"text/javascript\">(?<script>.*?)</script>",
            RegexOptions.Singleline).Groups["script"].Value;
        var scriptHash = "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)));

        scriptHash.ShouldBe(ExpectedScriptHash);
        result.Headers["Content-Security-Policy"].ShouldBe($"script-src '{scriptHash}'");
    }

    [Fact]
    public void emits_CSP_header_without_stale_meta_and_preserves_form_behavior()
    {
        var result = Bind(new TestSaml2Message());
        var document = new HtmlParser().ParseDocument(result.Content);
        var form = document.QuerySelector("form[name='samlPostBindingSubmit']") as IHtmlFormElement;

        result.Headers["Content-Security-Policy"].ShouldBe($"script-src '{ExpectedScriptHash}'");
        document.QuerySelector("meta[http-equiv='Content-Security-Policy']").ShouldBeNull();
        form.ShouldNotBeNull();
        form.GetAttribute("action").ShouldBe("https://idp.example.com/sso");
        form.GetAttribute("method").ShouldBe("post");
        var samlRequest = form.QuerySelector("input[name='SAMLRequest']") as IHtmlInputElement;
        samlRequest.ShouldNotBeNull();
        document.QuerySelector("script")!.TextContent.ShouldContain("document.forms.samlPostBindingSubmit.submit();");
        var continueButton = form.QuerySelector("input[type='submit']") as IHtmlInputElement;
        continueButton.ShouldNotBeNull();
        continueButton.Value.ShouldBe("Continue");
        XNode.DeepEquals(
            XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(samlRequest.Value))),
            XElement.Parse(TestSaml2Message.Xml)).ShouldBeTrue();
    }

    [Fact]
    public void HTML_encodes_attribute_values()
    {
        var message = new TestSaml2Message
        {
            DestinationUrl = new Uri("https://idp.example.com/sso?a=1&b=\"quoted\""),
            RelayState = "\"/><script>alert('relay')</script>",
            MessageName = "\"/><script>alert('name')</script>"
        };

        var result = Bind(message);

        result.Content.ShouldContain($"action=\"{WebUtility.HtmlEncode(message.DestinationUrl.OriginalString)}\"");
        result.Content.ShouldContain($"value=\"{WebUtility.HtmlEncode(message.RelayState)}\"");
        result.Content.ShouldContain($"name=\"{WebUtility.HtmlEncode(message.MessageName)}\"");
        result.Content.ShouldNotContain("<script>alert('relay')</script>");
        result.Content.ShouldNotContain("<script>alert('name')</script>");
    }

    private static Duende.IdentityServer.Internal.Saml.Sp.Commands.CommandResult Bind(TestSaml2Message message)
        => new Saml2PostBinding().Bind(message, logger: null);

    private sealed class TestSaml2Message : ISaml2Message
    {
        public const string Xml = "<samlp:AuthnRequest xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\"/>";

        public Uri DestinationUrl { get; init; } = new("https://idp.example.com/sso");
        public string MessageName { get; init; } = "SAMLRequest";
        public string RelayState { get; init; } = "relay-state";
        public X509Certificate2 SigningCertificate => null;
        public string SigningAlgorithm => null;
        public EntityId Issuer => null;

        public string ToXml() => Xml;

        public XElement ToXElement() => XElement.Parse(Xml);
    }
}
