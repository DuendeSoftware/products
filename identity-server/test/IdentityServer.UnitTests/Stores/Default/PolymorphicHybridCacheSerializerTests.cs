// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Buffers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Serialization;

namespace UnitTests.Stores.Default;

public class PolymorphicHybridCacheSerializerTests
{
    private readonly PolymorphicHybridCacheSerializer<IdentityProvider> _serializer;

    public PolymorphicHybridCacheSerializerTests()
    {
        var resolver = new PolymorphicJsonTypeResolver();
        var registration = resolver.AddPolymorphicType<IdentityProvider>("$type");
        registration.AddDerivedType<OidcProvider>("oidc");
        registration.AddDerivedType<SamlProvider>("saml");
        registration.AddDerivedType<TestCustomProvider>("test-custom");

        _serializer = new PolymorphicHybridCacheSerializer<IdentityProvider>(resolver);
    }

    [Fact]
    public void OidcProvider_round_trips()
    {
        var original = new OidcProvider
        {
            Scheme = "oidc-scheme",
            DisplayName = "OIDC",
            Authority = "https://idp.example.com",
            ClientId = "client-1",
            ResponseType = "code",
            Scope = "openid profile"
        };

        var deserialized = RoundTrip(original);

        deserialized.ShouldBeOfType<OidcProvider>();
        var oidc = (OidcProvider)deserialized;
        oidc.Scheme.ShouldBe("oidc-scheme");
        oidc.Authority.ShouldBe("https://idp.example.com");
        oidc.ClientId.ShouldBe("client-1");
        oidc.ResponseType.ShouldBe("code");
        oidc.Scope.ShouldBe("openid profile");
    }

    [Fact]
    public void SamlProvider_round_trips()
    {
        var original = new SamlProvider
        {
            Scheme = "saml-scheme",
            DisplayName = "SAML",
            IdpEntityId = "https://idp.example.com/entity",
            SingleSignOnServiceUrl = "https://idp.example.com/sso"
        };

        var deserialized = RoundTrip(original);

        deserialized.ShouldBeOfType<SamlProvider>();
        var saml = (SamlProvider)deserialized;
        saml.Scheme.ShouldBe("saml-scheme");
        saml.IdpEntityId.ShouldBe("https://idp.example.com/entity");
        saml.SingleSignOnServiceUrl.ShouldBe("https://idp.example.com/sso");
    }

    [Fact]
    public void Custom_derived_provider_round_trips()
    {
        var original = new TestCustomProvider
        {
            Scheme = "custom-scheme",
            DisplayName = "Custom",
            CustomProperty = "hello"
        };

        var deserialized = RoundTrip(original);

        deserialized.ShouldBeOfType<TestCustomProvider>();
        var custom = (TestCustomProvider)deserialized;
        custom.Scheme.ShouldBe("custom-scheme");
        custom.DisplayName.ShouldBe("Custom");
        custom.CustomProperty.ShouldBe("hello");
    }

    [Fact]
    public void Base_IdentityProvider_round_trips()
    {
        var original = new IdentityProvider("unknown-type")
        {
            Scheme = "base-scheme",
            DisplayName = "Base",
            Enabled = false
        };

        var deserialized = RoundTrip(original);

        deserialized.Type.ShouldBe("unknown-type");
        deserialized.Scheme.ShouldBe("base-scheme");
        deserialized.DisplayName.ShouldBe("Base");
        deserialized.Enabled.ShouldBe(false);
    }

    [Fact]
    public void Preserves_properties_dictionary()
    {
        var original = new OidcProvider
        {
            Scheme = "props-test",
            Authority = "https://example.com",
            Properties =
            {
                ["custom-key"] = "custom-value",
                ["another"] = "data"
            }
        };

        var deserialized = RoundTrip(original);

        var oidc = (OidcProvider)deserialized;
        oidc.Properties["custom-key"].ShouldBe("custom-value");
        oidc.Properties["another"].ShouldBe("data");
    }

    [Fact]
    public void Handles_null_optional_fields()
    {
        var original = new OidcProvider
        {
            Scheme = "minimal",
            Authority = "https://example.com"
        };

        var deserialized = RoundTrip(original);

        var oidc = (OidcProvider)deserialized;
        oidc.Scheme.ShouldBe("minimal");
        oidc.DisplayName.ShouldBeNull();
        oidc.ClientSecret.ShouldBeNull();
    }

    private IdentityProvider RoundTrip(IdentityProvider original)
    {
        var buffer = new ArrayBufferWriter<byte>();
        _serializer.Serialize(original, buffer);

        var sequence = new ReadOnlySequence<byte>(buffer.WrittenMemory);
        return _serializer.Deserialize(sequence);
    }

    private record TestCustomProvider : IdentityProvider
    {
        public TestCustomProvider() : base("test-custom") { }

        public TestCustomProvider(IdentityProvider other) : base("test-custom", other) { }

        public string? CustomProperty
        {
            get => this["CustomProperty"];
            set => this["CustomProperty"] = value;
        }
    }
}
