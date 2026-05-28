// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Text.Json;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;

namespace UnitTests.Stores.Default;

public class CachedTypeSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var resolver = new PolymorphicJsonTypeResolver();
        var registration = resolver.AddPolymorphicType<IdentityProvider>("$type");
        registration.AddDerivedType<OidcProvider>("oidc");
        registration.AddDerivedType<SamlProvider>("saml");
        registration.AddDerivedType<TestCustomProvider>("test-custom");

        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = false,
            TypeInfoResolver = resolver
        };
    }

    [Fact]
    public void Client_round_trips_through_json()
    {
        var original = new Client
        {
            ClientId = "test-client",
            ClientName = "Test Client",
            Enabled = true,
            AllowedGrantTypes = { "authorization_code" },
            ClientSecrets = { new Secret("secret".Sha256()) },
            RedirectUris = { "https://example.com/callback" },
            PostLogoutRedirectUris = { "https://example.com/signout" },
            AllowedScopes = { "openid", "profile" },
            Properties = { ["key"] = "value" },
            Claims = { new ClientClaim("sub", "123") },
            RequirePkce = true,
            AllowOfflineAccess = true
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Client>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.ClientId.ShouldBe(original.ClientId);
        deserialized.ClientName.ShouldBe(original.ClientName);
        deserialized.Enabled.ShouldBe(original.Enabled);
        deserialized.AllowedGrantTypes.ShouldContain("authorization_code");
        deserialized.ClientSecrets.Count.ShouldBe(1);
        deserialized.RedirectUris.ShouldContain("https://example.com/callback");
        deserialized.AllowedScopes.ShouldBe(original.AllowedScopes, ignoreOrder: true);
        deserialized.Properties["key"].ShouldBe("value");
        deserialized.RequirePkce.ShouldBe(true);
        deserialized.AllowOfflineAccess.ShouldBe(true);
    }

    [Fact]
    public void Resources_round_trips_through_json()
    {
        var original = new Resources
        {
            OfflineAccess = true,
            IdentityResources =
            {
                new IdentityResource
                {
                    Name = "openid",
                    DisplayName = "OpenID",
                    UserClaims = { "sub" }
                }
            },
            ApiResources =
            {
                new ApiResource
                {
                    Name = "api1",
                    DisplayName = "API 1",
                    Scopes = { "api1.read" },
                    ApiSecrets = { new Secret("secret".Sha256()) }
                }
            },
            ApiScopes =
            {
                new ApiScope
                {
                    Name = "api1.read",
                    DisplayName = "Read API 1"
                }
            }
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Resources>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.OfflineAccess.ShouldBe(true);
        deserialized.IdentityResources.Count.ShouldBe(1);
        deserialized.IdentityResources.First().Name.ShouldBe("openid");
        deserialized.IdentityResources.First().UserClaims.ShouldContain("sub");
        deserialized.ApiResources.Count.ShouldBe(1);
        deserialized.ApiResources.First().Name.ShouldBe("api1");
        deserialized.ApiResources.First().Scopes.ShouldContain("api1.read");
        deserialized.ApiScopes.Count.ShouldBe(1);
        deserialized.ApiScopes.First().Name.ShouldBe("api1.read");
    }

    [Fact]
    public void CorsCacheEntry_round_trips_through_json()
    {
        var original = new CachingCorsPolicyService<ICorsPolicyService>.CorsCacheEntry(true);

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CachingCorsPolicyService<ICorsPolicyService>.CorsCacheEntry>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.Allowed.ShouldBe(true);
    }

    [Fact]
    public void CorsCacheEntry_round_trips_when_false()
    {
        var original = new CachingCorsPolicyService<ICorsPolicyService>.CorsCacheEntry(false);

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CachingCorsPolicyService<ICorsPolicyService>.CorsCacheEntry>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.Allowed.ShouldBe(false);
    }

    [Fact]
    public void IdentityProvider_round_trips_through_json()
    {
        var original = new IdentityProvider("custom")
        {
            Scheme = "my-scheme",
            DisplayName = "My Provider",
            Enabled = true,
            Properties =
            {
                ["Authority"] = "https://example.com",
                ["ClientId"] = "client-123"
            }
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IdentityProvider>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.Type.ShouldBe("custom");
        deserialized.Scheme.ShouldBe("my-scheme");
        deserialized.DisplayName.ShouldBe("My Provider");
        deserialized.Enabled.ShouldBe(true);
        deserialized.Properties["Authority"].ShouldBe("https://example.com");
        deserialized.Properties["ClientId"].ShouldBe("client-123");
    }

    [Fact]
    public void OidcProvider_round_trips_through_json()
    {
        // This test simulates the HybridCache serialization path:
        // The EF store returns an OidcProvider, but HybridCache serializes it as
        // IdentityProvider (the declared type parameter in GetOrCreateAsync<IdentityProvider>).
        // The PolymorphicJsonTypeResolver must emit the "$type" discriminator so that
        // deserialization restores the concrete OidcProvider type.
        var original = new OidcProvider
        {
            Scheme = "oidc-provider",
            DisplayName = "OIDC Provider",
            Enabled = true,
            Authority = "https://idp.example.com",
            ClientId = "oidc-client",
            ClientSecret = "oidc-secret",
            ResponseType = "code",
            Scope = "openid profile",
            UsePkce = true,
            GetClaimsFromUserInfoEndpoint = true
        };

        // Serialize as the base type — this is what HybridCache does
        var json = JsonSerializer.Serialize<IdentityProvider>(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IdentityProvider>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<OidcProvider>();
        var oidc = (OidcProvider)deserialized;
        oidc.Scheme.ShouldBe("oidc-provider");
        oidc.DisplayName.ShouldBe("OIDC Provider");
        oidc.Authority.ShouldBe("https://idp.example.com");
        oidc.ClientId.ShouldBe("oidc-client");
        oidc.ClientSecret.ShouldBe("oidc-secret");
        oidc.ResponseType.ShouldBe("code");
        oidc.Scope.ShouldBe("openid profile");
        oidc.UsePkce.ShouldBe(true);
        oidc.GetClaimsFromUserInfoEndpoint.ShouldBe(true);
    }

    [Fact]
    public void IdentityProviderName_round_trips_through_json()
    {
        var original = new IdentityProviderName
        {
            Scheme = "test-scheme",
            DisplayName = "Test Scheme",
            Enabled = true
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IdentityProviderName>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.Scheme.ShouldBe("test-scheme");
        deserialized.DisplayName.ShouldBe("Test Scheme");
        deserialized.Enabled.ShouldBe(true);
    }

    [Fact]
    public void IdentityProviderName_collection_round_trips_through_json()
    {
        IReadOnlyCollection<IdentityProviderName> original =
        [
            new() { Scheme = "scheme1", DisplayName = "Scheme 1", Enabled = true },
            new() { Scheme = "scheme2", DisplayName = "Scheme 2", Enabled = false }
        ];

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IReadOnlyCollection<IdentityProviderName>>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.Count.ShouldBe(2);
        deserialized.ShouldContain(x => x.Scheme == "scheme1" && x.Enabled);
        deserialized.ShouldContain(x => x.Scheme == "scheme2" && !x.Enabled);
    }

    [Fact]
    public void Custom_derived_provider_round_trips_through_json()
    {
        var original = new TestCustomProvider
        {
            Scheme = "custom-scheme",
            DisplayName = "Custom Provider",
            Enabled = true,
            CustomProperty = "custom-value"
        };

        var json = JsonSerializer.Serialize<IdentityProvider>(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<IdentityProvider>(json, JsonOptions);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<TestCustomProvider>();
        var custom = (TestCustomProvider)deserialized;
        custom.Scheme.ShouldBe("custom-scheme");
        custom.DisplayName.ShouldBe("Custom Provider");
        custom.Enabled.ShouldBe(true);
        custom.CustomProperty.ShouldBe("custom-value");
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
