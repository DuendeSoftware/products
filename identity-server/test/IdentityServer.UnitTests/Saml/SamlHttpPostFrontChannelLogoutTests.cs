// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Internal.Saml.SingleLogout;
using Duende.IdentityServer.Models;

namespace UnitTests.Saml;

public class SamlHttpPostFrontChannelLogoutTests
{
    private const string Category = "SAML HTTP POST Front Channel Logout";

    [Fact]
    [Trait("Category", Category)]
    public void constructor_should_set_properties()
    {
        var destination = new Uri("https://sp.example.com/slo");
        var logoutRequest = "base64encodedrequest";
        var relayState = "state123";

        var subject = new SamlHttpPostFrontChannelLogout(destination, logoutRequest, relayState);

        subject.Destination.ShouldBe(destination);
        subject.EncodedContent.ShouldBe(logoutRequest);
        subject.SamlBinding.ShouldBe(SamlBinding.HttpPost);
        subject.RelayState.ShouldNotBeNull();
        subject.RelayState.ShouldBe(relayState);
    }

    [Fact]
    [Trait("Category", Category)]
    public void constructor_with_null_relay_state_should_set_relay_state_to_null()
    {
        var subject = new SamlHttpPostFrontChannelLogout(
            new Uri("https://sp.example.com/slo"),
            "base64encodedrequest",
            null);

        subject.RelayState.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void saml_binding_should_return_http_post()
    {
        var subject = new SamlHttpPostFrontChannelLogout(
            new Uri("https://sp.example.com/slo"),
            "base64encodedrequest",
            null);

        subject.SamlBinding.ShouldBe(SamlBinding.HttpPost);
    }

    [Fact]
    [Trait("Category", Category)]
    public void relay_state_should_parse_from_string()
    {
        var relayState = "mystate";
        var subject = new SamlHttpPostFrontChannelLogout(
            new Uri("https://sp.example.com/slo"),
            "base64encodedrequest",
            relayState);

        subject.RelayState.ShouldNotBeNull();
        subject.RelayState.ShouldBe(relayState);
    }
}
