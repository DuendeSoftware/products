// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.IdentityServer.Admin.IdentityResources;
using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.IdentityServer.Models;
using Duende.Storage.EntityAttributeValue;

namespace IdentityServer.UnitTests.Admin;

/// <summary>
/// Reflection- and instance-based tests that pin the shape of the six admin entity model triads
/// (<c>*Configuration</c>/<c>Create*</c>/<c>Update*</c>/<c>*ListItem</c>) introduced/finalized by
/// issue #3427 (plan tasks 1-9). These tests exist to catch regressions such as a parent model
/// regaining an entity <c>Id</c>/<c>Version</c>, a read model gaining a plain mutable setter, or a
/// write model losing its mutability -- none of which would necessarily be caught by the
/// integration test suite, which only exercises the admin implementations through their public
/// interfaces.
/// </summary>
public class AdminApiContractTests
{
    /// <summary>
    /// Describes the five related types for one admin entity: the immutable read model
    /// (<c>Configuration</c>), the two mutable write models (<c>Create</c>/<c>Update</c>),
    /// the lightweight list/query projection (<c>ListItem</c>), and the typed storage identifier.
    /// </summary>
    public sealed record EntityShape(
        string Name,
        Type Configuration,
        Type Create,
        Type Update,
        Type ListItem,
        Type Id);

    public static TheoryData<EntityShape> Entities => new()
    {
        new EntityShape(
            "ApiResource",
            typeof(ApiResourceConfiguration),
            typeof(CreateApiResource),
            typeof(UpdateApiResource),
            typeof(ApiResourceListItem),
            typeof(ApiResourceId)),
        new EntityShape(
            "ApiScope",
            typeof(ApiScopeConfiguration),
            typeof(CreateApiScope),
            typeof(UpdateApiScope),
            typeof(ApiScopeListItem),
            typeof(ApiScopeId)),
        new EntityShape(
            "Client",
            typeof(ClientConfiguration),
            typeof(CreateClient),
            typeof(UpdateClient),
            typeof(ClientListItem),
            typeof(ClientId)),
        new EntityShape(
            "IdentityProvider",
            typeof(IdentityProviderConfiguration),
            typeof(CreateIdentityProvider),
            typeof(UpdateIdentityProvider),
            typeof(IdentityProviderListItem),
            typeof(IdentityProviderId)),
        new EntityShape(
            "IdentityResource",
            typeof(IdentityResourceConfiguration),
            typeof(CreateIdentityResource),
            typeof(UpdateIdentityResource),
            typeof(IdentityResourceListItem),
            typeof(IdentityResourceId)),
        new EntityShape(
            "SamlServiceProvider",
            typeof(SamlServiceProviderConfiguration),
            typeof(CreateSamlServiceProvider),
            typeof(UpdateSamlServiceProvider),
            typeof(SamlServiceProviderListItem),
            typeof(SamlServiceProviderId)),
    };

    // === 1. Configuration sealing / setter shape ===

    [Theory]
    [MemberData(nameof(Entities))]
    public void Configuration_is_sealed_and_has_no_plain_mutable_setters(EntityShape shape)
    {
        shape.Configuration.IsSealed.ShouldBeTrue($"{shape.Configuration.Name} should be sealed.");

        var mutableProperties = shape.Configuration
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is not null && !IsInitOnly(p))
            .Select(p => p.Name)
            .ToList();

        mutableProperties.ShouldBeEmpty(
            $"{shape.Configuration.Name} should only expose init-only (or no) setters, " +
            $"but found plain mutable setters: {string.Join(", ", mutableProperties)}");
    }

    // === 2. No entity Id/Version on the top-level Configuration ===

    [Theory]
    [MemberData(nameof(Entities))]
    public void Configuration_has_no_top_level_id_or_version_property(EntityShape shape)
    {
        var propertyNames = shape.Configuration
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        propertyNames.ShouldNotContain("Id",
            $"{shape.Configuration.Name} must not carry the entity's storage identifier " +
            "-- it lives only in admin interface method signatures/results.");
        propertyNames.ShouldNotContain("Version",
            $"{shape.Configuration.Name} must not carry a concurrency version " +
            "-- it is returned separately by Get/Query results.");
    }

    // === 3. Create/Update are sealed and mutable ===

    [Theory]
    [MemberData(nameof(Entities))]
    public void Create_and_update_models_are_sealed_and_mutable(EntityShape shape)
    {
        foreach (var writeModelType in new[] { shape.Create, shape.Update })
        {
            writeModelType.IsSealed.ShouldBeTrue($"{writeModelType.Name} should be sealed.");

            var hasMutableProperty = writeModelType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.SetMethod is not null && !IsInitOnly(p));

            hasMutableProperty.ShouldBeTrue(
                $"{writeModelType.Name} should expose at least one plain mutable (non-init) setter.");
        }
    }

    // === 4. ToCreate()/ToUpdate() exist with the correct return types ===

    [Theory]
    [MemberData(nameof(Entities))]
    public void Configuration_exposes_ToCreate_and_ToUpdate(EntityShape shape)
    {
        var toUpdate = shape.Configuration.GetMethod("ToUpdate", BindingFlags.Public | BindingFlags.Instance);
        toUpdate.ShouldNotBeNull($"{shape.Configuration.Name} should expose a public instance ToUpdate() method.");
        toUpdate!.GetParameters().ShouldBeEmpty();
        toUpdate.ReturnType.ShouldBe(shape.Update);

        var toCreate = shape.Configuration.GetMethod("ToCreate", BindingFlags.Public | BindingFlags.Instance);
        toCreate.ShouldNotBeNull($"{shape.Configuration.Name} should expose a public instance ToCreate() method.");
        toCreate!.GetParameters().ShouldBeEmpty();
        toCreate.ReturnType.ShouldBe(shape.Create);
    }

    // === 7. ListItem.Id uses the typed ID, not a bare Guid ===

    [Theory]
    [MemberData(nameof(Entities))]
    public void ListItem_id_property_uses_typed_id(EntityShape shape)
    {
        var idProperty = shape.ListItem.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        idProperty.ShouldNotBeNull($"{shape.ListItem.Name} should expose an Id property.");
        idProperty!.PropertyType.ShouldBe(shape.Id);
    }

    // === 5. Read-only vs. mutable collection type shapes for a representative sample ===

    [Fact]
    public void ApiResource_collections_are_read_only_on_configuration_and_mutable_on_write_models()
    {
        typeof(ApiResourceConfiguration).GetProperty(nameof(ApiResourceConfiguration.UserClaims))!
            .PropertyType.ShouldBe(typeof(IReadOnlyList<string>));
        typeof(CreateApiResource).GetProperty(nameof(CreateApiResource.UserClaims))!
            .PropertyType.ShouldBe(typeof(List<string>));
        typeof(UpdateApiResource).GetProperty(nameof(UpdateApiResource.UserClaims))!
            .PropertyType.ShouldBe(typeof(List<string>));

        typeof(ApiResourceConfiguration).GetProperty(nameof(ApiResourceConfiguration.ExtendedProperties))!
            .PropertyType.ShouldBe(typeof(IReadOnlyCollection<AttributeValue>));
        typeof(CreateApiResource).GetProperty(nameof(CreateApiResource.ExtendedProperties))!
            .PropertyType.ShouldBe(typeof(AttributeValueCollection));
        typeof(UpdateApiResource).GetProperty(nameof(UpdateApiResource.ExtendedProperties))!
            .PropertyType.ShouldBe(typeof(AttributeValueCollection));
    }

    [Fact]
    public void SamlServiceProvider_dictionary_and_list_collections_are_read_only_on_configuration()
    {
        typeof(SamlServiceProviderConfiguration).GetProperty(nameof(SamlServiceProviderConfiguration.ClaimMappings))!
            .PropertyType.ShouldBe(typeof(IReadOnlyDictionary<string, string>));
        typeof(UpdateSamlServiceProvider).GetProperty(nameof(UpdateSamlServiceProvider.ClaimMappings))!
            .PropertyType.ShouldBe(typeof(Dictionary<string, string>));

        typeof(SamlServiceProviderConfiguration).GetProperty(nameof(SamlServiceProviderConfiguration.Certificates))!
            .PropertyType.ShouldBe(typeof(IReadOnlyList<SamlCertificateConfiguration>));
        typeof(UpdateSamlServiceProvider).GetProperty(nameof(UpdateSamlServiceProvider.Certificates))!
            .PropertyType.ShouldBe(typeof(List<SamlCertificateInput>));
    }

    [Fact]
    public void Client_extended_properties_use_AttributeValueCollection_on_both_read_and_write_models()
    {
        // Per task 8's decision, Client's ExtendedProperties is AttributeValueCollection on the
        // read model too (unlike the other five entities, which use IReadOnlyCollection<AttributeValue>
        // for their read model). This is intentional -- pin it so it doesn't regress or get "fixed"
        // into inconsistency with the other entities.
        typeof(ClientConfiguration).GetProperty(nameof(ClientConfiguration.ExtendedProperties))!
            .PropertyType.ShouldBe(typeof(AttributeValueCollection));
        typeof(CreateClient).GetProperty(nameof(CreateClient.ExtendedProperties))!
            .PropertyType.ShouldBe(typeof(AttributeValueCollection));
        typeof(UpdateClient).GetProperty(nameof(UpdateClient.ExtendedProperties))!
            .PropertyType.ShouldBe(typeof(AttributeValueCollection));
    }

    // === 8. SAML certificate ID stays untyped Guid; API resource secret ID is typed (asymmetry) ===

    [Fact]
    public void Saml_certificate_id_remains_untyped_guid_by_design()
    {
        typeof(SamlCertificateConfiguration).GetProperty(nameof(SamlCertificateConfiguration.Id))!
            .PropertyType.ShouldBe(typeof(Guid));
        typeof(SamlCertificateInput).GetProperty(nameof(SamlCertificateInput.Id))!
            .PropertyType.ShouldBe(typeof(Guid));
    }

    [Fact]
    public void Api_resource_secret_id_is_typed_by_design() =>
        typeof(ApiResourceSecretConfiguration).GetProperty(nameof(ApiResourceSecretConfiguration.Id))!
            .PropertyType.ShouldBe(typeof(ApiResourceSecretId));

    // === 5 (continued). AttributeValueCollection defensive-copy behavior via ToUpdate()/ToCreate() ===

    [Fact]
    public void ApiScope_ToUpdate_extended_properties_is_a_defensive_copy()
    {
        AttributeCode code = "test_attr";
        var config = new ApiScopeConfiguration
        {
            Name = "scope1",
            ExtendedProperties = [AttributeValue.Load(code, "original")]
        };

        var update = config.ToUpdate();
        update.ExtendedProperties.Set(code, "mutated");

        config.ExtendedProperties.Single().TryGetValue<string>(out var originalValue).ShouldBeTrue();
        originalValue.ShouldBe("original");
    }

    [Fact]
    public void Client_ToUpdate_extended_properties_is_a_defensive_copy()
    {
        AttributeCode code = "test_attr";
        var extended = new AttributeValueCollection();
        extended.Set(code, "original");

        var config = new ClientConfiguration
        {
            ClientId = "client1",
            ExtendedProperties = extended
        };

        var update = config.ToUpdate();
        update.ExtendedProperties.Set(code, "mutated");

        config.ExtendedProperties[code].TryGetValue<string>(out var originalValue).ShouldBeTrue();
        originalValue.ShouldBe("original");

        // Mutating the ClientConfiguration's own AttributeValueCollection instance directly (not via
        // ToUpdate) is expected to work -- it is a mutable collection type even on the read model
        // (see Client_extended_properties_use_AttributeValueCollection_on_both_read_and_write_models).
        // The important guarantee pinned here is that ToUpdate() hands back an independent copy.
        var create = config.ToCreate();
        create.ExtendedProperties.Set(code, "mutated-again");
        config.ExtendedProperties[code].TryGetValue<string>(out var stillOriginalValue).ShouldBeTrue();
        stillOriginalValue.ShouldBe("original");
    }

    // === 6. Round-trip completeness + defensive copies for complex entities ===

    [Fact]
    public void ApiResource_ToUpdate_and_ToCreate_are_field_complete_and_defensive_copies()
    {
        AttributeCode code = "attr1";
        var config = new ApiResourceConfiguration
        {
            Name = "api1",
            Enabled = false,
            DisplayName = "Display",
            Description = "Desc",
            ShowInDiscoveryDocument = false,
            RequireResourceIndicator = true,
            UserClaims = ["sub", "email"],
            Scopes = ["scope1", "scope2"],
            AllowedAccessTokenSigningAlgorithms = ["RS256"],
            ApiSecrets = [new ApiResourceSecretConfiguration { Id = ApiResourceSecretId.New(), Type = "SharedSecret" }],
            ExtendedProperties = [AttributeValue.Load(code, "v1")]
        };

        var update = config.ToUpdate();
        update.Name.ShouldBe(config.Name);
        update.Enabled.ShouldBe(config.Enabled);
        update.DisplayName.ShouldBe(config.DisplayName);
        update.Description.ShouldBe(config.Description);
        update.ShowInDiscoveryDocument.ShouldBe(config.ShowInDiscoveryDocument);
        update.RequireResourceIndicator.ShouldBe(config.RequireResourceIndicator);
        update.UserClaims.ShouldBe(config.UserClaims);
        update.Scopes.ShouldBe(config.Scopes);
        update.AllowedAccessTokenSigningAlgorithms.ShouldBe(config.AllowedAccessTokenSigningAlgorithms);
        update.ExtendedProperties.Count.ShouldBe(1);

        update.UserClaims.Add("mutated");
        update.Scopes.Add("mutated");
        update.AllowedAccessTokenSigningAlgorithms.Add("mutated");
        update.ExtendedProperties.Set(code, "mutated");

        config.UserClaims.Count.ShouldBe(2);
        config.Scopes.Count.ShouldBe(2);
        config.AllowedAccessTokenSigningAlgorithms.Count.ShouldBe(1);
        config.ExtendedProperties.Single().TryGetValue<string>(out var originalValue).ShouldBeTrue();
        originalValue.ShouldBe("v1");

        var create = config.ToCreate();
        create.Name.ShouldBe(config.Name);
        create.Enabled.ShouldBe(config.Enabled);
        create.DisplayName.ShouldBe(config.DisplayName);
        create.Description.ShouldBe(config.Description);
        create.ShowInDiscoveryDocument.ShouldBe(config.ShowInDiscoveryDocument);
        create.RequireResourceIndicator.ShouldBe(config.RequireResourceIndicator);
        create.UserClaims.ShouldBe(config.UserClaims);
        create.Scopes.ShouldBe(config.Scopes);
        create.AllowedAccessTokenSigningAlgorithms.ShouldBe(config.AllowedAccessTokenSigningAlgorithms);
        create.ExtendedProperties.Count.ShouldBe(1);

        create.UserClaims.Add("mutated2");
        config.UserClaims.Count.ShouldBe(2);
    }

    [Fact]
    public void SamlServiceProvider_ToUpdate_and_ToCreate_are_field_complete_and_defensive_copies()
    {
        AttributeCode code = "sp_attr";
        var certId = Guid.NewGuid();
        var config = new SamlServiceProviderConfiguration
        {
            EntityId = "urn:sp1",
            Enabled = false,
            DisplayName = "SP Display",
            Description = "SP Desc",
            ClockSkew = TimeSpan.FromMinutes(2),
            RequestMaxAge = TimeSpan.FromMinutes(10),
            AssertionLifetime = TimeSpan.FromMinutes(5),
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            SingleLogoutServiceUrls =
            [
                new SamlEndpointConfiguration { Location = "https://sp/slo", Binding = SamlBinding.HttpRedirect }
            ],
            RequireSignedAuthnRequests = true,
            RequireSignedLogoutResponses = true,
            Certificates =
            [
                new SamlCertificateConfiguration
                {
                    Id = certId,
                    Base64Data = "AAAA",
                    Use = KeyUse.Signing,
                    Subject = "CN=Test",
                    Thumbprint = "ABC123",
                    NotAfter = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            ],
            AllowIdpInitiated = true,
            AllowedScopes = ["scope1"],
            ClaimMappings = new Dictionary<string, string> { ["sub"] = "NameID" },
            AuthnContextMappings = new Dictionary<string, string> { ["pwd"] = "urn:oasis:names:tc:SAML:2.0:ac:classes:Password" },
            RequestedClaimTypes = ["email"],
            DefaultNameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            EmailNameIdClaimType = "email",
            SigningBehavior = SamlSigningBehavior.SignBoth,
            AllowedSignatureAlgorithms = ["RSA-SHA256"],
            ExtendedProperties = [AttributeValue.Load(code, "v1")]
        };

        var update = config.ToUpdate();

        update.EntityId.ShouldBe(config.EntityId);
        update.Enabled.ShouldBe(config.Enabled);
        update.DisplayName.ShouldBe(config.DisplayName);
        update.Description.ShouldBe(config.Description);
        update.ClockSkew.ShouldBe(config.ClockSkew);
        update.RequestMaxAge.ShouldBe(config.RequestMaxAge);
        update.AssertionLifetime.ShouldBe(config.AssertionLifetime);
        update.AssertionConsumerServiceUrls.Count.ShouldBe(1);
        update.AssertionConsumerServiceUrls[0].Location.ShouldBe("https://sp/acs");
        update.SingleLogoutServiceUrls.Count.ShouldBe(1);
        update.RequireSignedAuthnRequests.ShouldBe(config.RequireSignedAuthnRequests);
        update.RequireSignedLogoutResponses.ShouldBe(config.RequireSignedLogoutResponses);
        update.Certificates.Count.ShouldBe(1);
        update.Certificates[0].Id.ShouldBe(certId);
        update.Certificates[0].Base64Data.ShouldBe("AAAA");
        update.AllowIdpInitiated.ShouldBe(config.AllowIdpInitiated);
        update.AllowedScopes.ShouldBe(config.AllowedScopes);
        update.ClaimMappings.Count.ShouldBe(config.ClaimMappings.Count);
        update.ClaimMappings["sub"].ShouldBe(config.ClaimMappings["sub"]);
        update.AuthnContextMappings.Count.ShouldBe(config.AuthnContextMappings.Count);
        update.AuthnContextMappings["pwd"].ShouldBe(config.AuthnContextMappings["pwd"]);
        update.RequestedClaimTypes.ShouldBe(config.RequestedClaimTypes);
        update.DefaultNameIdFormat.ShouldBe(config.DefaultNameIdFormat);
        update.EmailNameIdClaimType.ShouldBe(config.EmailNameIdClaimType);
        update.SigningBehavior.ShouldBe(config.SigningBehavior);
        update.AllowedSignatureAlgorithms.ShouldBe(config.AllowedSignatureAlgorithms);
        update.ExtendedProperties.Count.ShouldBe(1);

        update.AssertionConsumerServiceUrls.Add(new SamlIndexedEndpointConfiguration
        {
            Location = "https://mutated",
            Binding = SamlBinding.HttpPost
        });
        update.SingleLogoutServiceUrls.Add(new SamlEndpointConfiguration
        {
            Location = "https://mutated",
            Binding = SamlBinding.HttpRedirect
        });
        update.Certificates.Add(new SamlCertificateInput { Base64Data = "BBBB" });
        update.AllowedScopes.Add("mutated");
        update.ClaimMappings["mutated"] = "mutated";
        update.AuthnContextMappings["mutated"] = "mutated";
        update.RequestedClaimTypes.Add("mutated");
        update.AllowedSignatureAlgorithms.Add("mutated");
        update.ExtendedProperties.Set(code, "mutated");

        config.AssertionConsumerServiceUrls.Count.ShouldBe(1);
        config.SingleLogoutServiceUrls.Count.ShouldBe(1);
        config.Certificates.Count.ShouldBe(1);
        config.AllowedScopes.Count.ShouldBe(1);
        config.ClaimMappings.Count.ShouldBe(1);
        config.AuthnContextMappings.Count.ShouldBe(1);
        config.RequestedClaimTypes.Count.ShouldBe(1);
        config.AllowedSignatureAlgorithms.Count.ShouldBe(1);
        config.ExtendedProperties.Single().TryGetValue<string>(out var originalValue).ShouldBeTrue();
        originalValue.ShouldBe("v1");

        var create = config.ToCreate();
        create.EntityId.ShouldBe(config.EntityId);
        create.Certificates.Count.ShouldBe(1);

        create.Certificates.Add(new SamlCertificateInput { Base64Data = "CCCC" });
        config.Certificates.Count.ShouldBe(1);
    }

    [Fact]
    public void Client_ToUpdate_and_ToCreate_are_field_complete_and_defensive_copies()
    {
        AttributeCode code = "client_attr";
        var config = new ClientConfiguration
        {
            ClientId = "client1",
            Enabled = false,
            ClientName = "Client Name",
            Description = "Client Desc",
            AllowedGrantTypes = ["authorization_code"],
            AllowedScopes = ["scope1"],
            RedirectUris = ["https://client/callback"],
            PostLogoutRedirectUris = ["https://client/logout"],
            AllowedIdentityTokenSigningAlgorithms = ["RS256"],
            IdentityProviderRestrictions = ["idp1"],
            AllowedCorsOrigins = ["https://client"],
            Claims = [new ClientClaimConfiguration { Type = "custom", Value = "value1" }],
            ClientSecrets = [new ClientSecretConfiguration { Id = SecretId.New(), Type = "SharedSecret" }],
            ExtendedProperties = BuildAttributeValueCollection(code, "v1")
        };

        var update = config.ToUpdate();
        update.ClientId.ShouldBe(config.ClientId);
        update.Enabled.ShouldBe(config.Enabled);
        update.ClientName.ShouldBe(config.ClientName);
        update.Description.ShouldBe(config.Description);
        update.AllowedGrantTypes.ShouldBe(config.AllowedGrantTypes);
        update.AllowedScopes.ShouldBe(config.AllowedScopes);
        update.RedirectUris.ShouldBe(config.RedirectUris);
        update.PostLogoutRedirectUris.ShouldBe(config.PostLogoutRedirectUris);
        update.AllowedIdentityTokenSigningAlgorithms.ShouldBe(config.AllowedIdentityTokenSigningAlgorithms);
        update.IdentityProviderRestrictions.ShouldBe(config.IdentityProviderRestrictions);
        update.AllowedCorsOrigins.ShouldBe(config.AllowedCorsOrigins);
        update.Claims.Count.ShouldBe(1);
        update.Claims[0].Type.ShouldBe("custom");
        update.Claims[0].Value.ShouldBe("value1");

        update.AllowedGrantTypes.Add("mutated");
        update.AllowedScopes.Add("mutated");
        update.RedirectUris.Add("mutated");
        update.PostLogoutRedirectUris.Add("mutated");
        update.AllowedIdentityTokenSigningAlgorithms.Add("mutated");
        update.IdentityProviderRestrictions.Add("mutated");
        update.AllowedCorsOrigins.Add("mutated");
        update.Claims.Add(new ClientClaimConfiguration { Type = "mutated", Value = "mutated" });
        update.ExtendedProperties.Set(code, "mutated");

        config.AllowedGrantTypes.Count.ShouldBe(1);
        config.AllowedScopes.Count.ShouldBe(1);
        config.RedirectUris.Count.ShouldBe(1);
        config.PostLogoutRedirectUris.Count.ShouldBe(1);
        config.AllowedIdentityTokenSigningAlgorithms.Count.ShouldBe(1);
        config.IdentityProviderRestrictions.Count.ShouldBe(1);
        config.AllowedCorsOrigins.Count.ShouldBe(1);
        config.Claims.Count.ShouldBe(1);
        config.ExtendedProperties[code].TryGetValue<string>(out var originalValue).ShouldBeTrue();
        originalValue.ShouldBe("v1");

        var create = config.ToCreate();
        create.ClientId.ShouldBe(config.ClientId);
        create.AllowedGrantTypes.Count.ShouldBe(1);

        create.AllowedGrantTypes.Add("mutated2");
        config.AllowedGrantTypes.Count.ShouldBe(1);
    }

    private static AttributeValueCollection BuildAttributeValueCollection(AttributeCode code, string value)
    {
        var collection = new AttributeValueCollection();
        collection.Set(code, value);
        return collection;
    }

    private static bool IsInitOnly(PropertyInfo property)
    {
        var setMethod = property.SetMethod;
        if (setMethod is null)
        {
            return true;
        }

        return setMethod.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(t => t == typeof(IsExternalInit));
    }
}
