// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace UnitTests.Models;

public class SamlProviderTests
{
    private const string Category = "SamlProvider Model Tests";

    [Fact]
    [Trait("Category", Category)]
    public void constructor_should_set_type_to_saml()
    {
        var provider = new SamlProvider();
        provider.Type.ShouldBe("saml");
    }

    [Fact]
    [Trait("Category", Category)]
    public void copy_constructor_should_copy_base_properties()
    {
        var source = new SamlProvider
        {
            Scheme = "test-scheme",
            DisplayName = "Test IdP",
            Enabled = true,
            IdpEntityId = "https://idp.example.com",
            SingleSignOnServiceUrl = "https://idp.example.com/sso",
        };

        var copy = new SamlProvider(source);

        copy.Type.ShouldBe("saml");
        copy.Scheme.ShouldBe("test-scheme");
        copy.DisplayName.ShouldBe("Test IdP");
        copy.Enabled.ShouldBeTrue();
        copy.IdpEntityId.ShouldBe("https://idp.example.com");
        copy.SingleSignOnServiceUrl.ShouldBe("https://idp.example.com/sso");
    }

    [Fact]
    [Trait("Category", Category)]
    public void copy_constructor_should_produce_independent_properties_dictionary()
    {
        var source = new SamlProvider { IdpEntityId = "https://idp.example.com" };
        var copy = new SamlProvider(source);

        copy.IdpEntityId = "https://other.example.com";

        source.IdpEntityId.ShouldBe("https://idp.example.com");
        copy.IdpEntityId.ShouldBe("https://other.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public void idp_entity_id_should_round_trip_through_properties()
    {
        var provider = new SamlProvider();
        provider.IdpEntityId = "https://idp.example.com";
        provider.IdpEntityId.ShouldBe("https://idp.example.com");
        provider.Properties.ShouldContainKey("IdpEntityId");
    }

    [Fact]
    [Trait("Category", Category)]
    public void single_sign_on_service_url_should_round_trip()
    {
        var provider = new SamlProvider();
        provider.SingleSignOnServiceUrl = "https://idp.example.com/sso";
        provider.SingleSignOnServiceUrl.ShouldBe("https://idp.example.com/sso");
    }

    [Fact]
    [Trait("Category", Category)]
    public void single_logout_service_url_should_round_trip()
    {
        var provider = new SamlProvider();
        provider.SingleLogoutServiceUrl = "https://idp.example.com/slo";
        provider.SingleLogoutServiceUrl.ShouldBe("https://idp.example.com/slo");
    }

    [Fact]
    [Trait("Category", Category)]
    public void signing_certificate_base64_should_round_trip()
    {
        var provider = new SamlProvider();
        provider.SigningCertificateBase64 = "ABCD1234=";
        provider.SigningCertificateBase64.ShouldBe("ABCD1234=");
    }

    [Fact]
    [Trait("Category", Category)]
    public void binding_type_should_default_to_redirect()
    {
        var provider = new SamlProvider();
        provider.BindingType.ShouldBe("redirect");
    }

    [Fact]
    [Trait("Category", Category)]
    public void binding_type_should_round_trip()
    {
        var provider = new SamlProvider();
        provider.BindingType = "post";
        provider.BindingType.ShouldBe("post");
    }

    [Fact]
    [Trait("Category", Category)]
    public void sp_entity_id_should_round_trip()
    {
        var provider = new SamlProvider();
        provider.SpEntityId = "https://sp.example.com";
        provider.SpEntityId.ShouldBe("https://sp.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public void allow_unsolicited_authn_response_should_default_to_false()
    {
        var provider = new SamlProvider();
        provider.AllowUnsolicitedAuthnResponse.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public void allow_unsolicited_authn_response_should_round_trip()
    {
        var provider = new SamlProvider();
        provider.AllowUnsolicitedAuthnResponse = true;
        provider.AllowUnsolicitedAuthnResponse.ShouldBeTrue();

        provider.AllowUnsolicitedAuthnResponse = false;
        provider.AllowUnsolicitedAuthnResponse.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public void want_assertions_signed_should_default_to_true()
    {
        var provider = new SamlProvider();
        provider.WantAssertionsSigned.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void want_assertions_signed_should_round_trip()
    {
        var provider = new SamlProvider();
        provider.WantAssertionsSigned = false;
        provider.WantAssertionsSigned.ShouldBeFalse();

        provider.WantAssertionsSigned = true;
        provider.WantAssertionsSigned.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void outbound_signing_algorithm_should_default_to_rsa_sha256()
    {
        var provider = new SamlProvider();
        provider.OutboundSigningAlgorithm.ShouldBe("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256");
    }

    [Fact]
    [Trait("Category", Category)]
    public void outbound_signing_algorithm_should_round_trip()
    {
        var provider = new SamlProvider();
        provider.OutboundSigningAlgorithm = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha512";
        provider.OutboundSigningAlgorithm.ShouldBe("http://www.w3.org/2001/04/xmldsig-more#rsa-sha512");
    }
}
