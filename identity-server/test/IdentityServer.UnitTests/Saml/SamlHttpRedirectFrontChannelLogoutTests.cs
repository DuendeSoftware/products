// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Internal.Saml.SingleLogout;
using Duende.IdentityServer.Models;

namespace UnitTests.Saml;

public class SamlHttpRedirectFrontChannelLogoutTests
{
    private const string Category = "SAML HTTP Redirect Front Channel Logout";

    [Fact]
    [Trait("Category", Category)]
    public void constructor_should_set_properties()
    {
        var destination = new Uri("https://sp.example.com/slo");
        var encodedContent = "?SAMLRequest=abc123&SigAlg=xyz&Signature=sig";

        var subject = new SamlHttpRedirectFrontChannelLogout(destination, encodedContent);

        subject.Destination.ShouldBe(destination);
        subject.EncodedContent.ShouldBe(encodedContent);
        subject.SamlBinding.ShouldBe(SamlBinding.HttpRedirect);
        subject.RelayState.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void saml_binding_should_return_http_redirect()
    {
        var subject = new SamlHttpRedirectFrontChannelLogout(
            new Uri("https://sp.example.com/slo"),
            "?SAMLRequest=abc");

        subject.SamlBinding.ShouldBe(SamlBinding.HttpRedirect);
    }

    [Fact]
    [Trait("Category", Category)]
    public void relay_state_should_always_return_null()
    {
        var subject = new SamlHttpRedirectFrontChannelLogout(
            new Uri("https://sp.example.com/slo"),
            "?SAMLRequest=abc");

        subject.RelayState.ShouldBeNull();
    }
}
