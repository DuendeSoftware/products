// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin.IdentityProviders;

public sealed class IdentityProviderExtendedPropertiesTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    // ── Schema validation ──────────────────────────────────────────────────

    [Fact]
    public async Task create_with_unknown_attribute_returns_validation_error()
    {
        var admin = _fixture.IdentityProviderAdmin;
        var provider = new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "oidc"
        };
        provider.ExtendedProperties.Set(AttributeCode.Create("bogus_attribute"), "value");

        var result = await admin.CreateAsync(provider, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
        var error = result.Errors.First(e => e.Code == "validation_failed");
        error.Message.ShouldContain("bogus_attribute");
    }

    [Fact]
    public async Task update_path_rejects_unknown_attribute()
    {
        var admin = _fixture.IdentityProviderAdmin;
        var provider = new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "oidc"
        };

        var createResult = await admin.CreateAsync(provider, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item.ToUpdate();
        toUpdate.ExtendedProperties.Set(AttributeCode.Create("bad_attr"), "value");

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);

        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldNotBeNull();
        updateResult.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task empty_extended_properties_bypasses_validation_for_any_type()
    {
        var admin = _fixture.IdentityProviderAdmin;

        // Create providers of different types with no extended properties - all should succeed
        var oidcResult = await admin.CreateAsync(new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "oidc"
        }, _ct);
        oidcResult.IsSuccess.ShouldBeTrue();

        var samlResult = await admin.CreateAsync(new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "saml"
        }, _ct);
        samlResult.IsSuccess.ShouldBeTrue();

        var unknownResult = await admin.CreateAsync(new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "custom_unknown_type"
        }, _ct);
        unknownResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task non_registered_provider_type_with_properties_fails_when_no_schema_registered()
    {
        var admin = _fixture.IdentityProviderAdmin;

        var provider = new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "custom_unregistered"
        };
        provider.ExtendedProperties.Set(AttributeCode.Create("SomeAttr"), "value");

        var result = await admin.CreateAsync(provider, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
        var error = result.Errors.First(e => e.Code == "validation_failed");
        error.Message.ShouldContain("SomeAttr");
        error.Message.ShouldContain("is not defined in the schema");
    }

    [Fact]
    public async Task create_with_extended_properties_fails_when_no_schema_configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var dbName = $"test_{Guid.NewGuid():N}";

        // Override the default schema store with an empty one to simulate a deployment
        // with no OIDC schema registered.
        services.AddIdentityServer()
            .AddStorage(storage =>
                storage.AddSqliteStore(opt =>
                    opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"))
            .AddIdentityProviderConfigurationValidator<NopIdentityProviderConfigurationValidator>();

        services.AddSingleton<ISchemaStore>(new InMemorySchemaStore([]));

        await using var provider = services.BuildServiceProvider();
        var schema = provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

        using var serviceScope = provider.CreateScope();
        var admin = serviceScope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        var configuration = new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "oidc"
        };
        configuration.ExtendedProperties.Set(AttributeCode.Create("Authority"), "https://idp.example.com");

        var result = await admin.CreateAsync(configuration, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    [Fact]
    public async Task user_schema_overrides_built_in_oidc_schema()
    {
        // The test fixture registers TestIdentityProviderAttributes.Schema with SchemaId idp:oidc
        // which extends the built-in schema with TenantId. If the override didn't work,
        // TenantId would be rejected as an unknown attribute.
        var admin = _fixture.IdentityProviderAdmin;

        var provider = new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "oidc"
        };
        provider.ExtendedProperties.Set(TestIdentityProviderAttributes.TenantId, "override-works");

        var result = await admin.CreateAsync(provider, _ct);
        result.IsSuccess.ShouldBeTrue($"Create failed - schema override not working: {result}");

        var getResult = await admin.GetAsync(result.Id, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item.ExtendedProperties.TryGet(TestIdentityProviderAttributes.TenantId.Code, out var attr).ShouldBeTrue();
        attr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("override-works");
    }

    // ── OIDC provider round-trip ───────────────────────────────────────────

    [Fact]
    public async Task extended_properties_round_trip_after_create()
    {
        var admin = _fixture.IdentityProviderAdmin;
        var provider = new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "oidc"
        };
        provider.ExtendedProperties.Set(AttributeCode.Create("Authority"), "https://idp.example.com");
        provider.ExtendedProperties.Set(AttributeCode.Create("ClientId"), "my-client");
        provider.ExtendedProperties.Set(TestIdentityProviderAttributes.TenantId, "acme");

        var createResult = await admin.CreateAsync(provider, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item;
        loaded.ExtendedProperties.Count.ShouldBe(3);

        loaded.ExtendedProperties.TryGet(AttributeCode.Create("Authority"), out var authorityAttr).ShouldBeTrue();
        authorityAttr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("https://idp.example.com");

        loaded.ExtendedProperties.TryGet(AttributeCode.Create("ClientId"), out var clientIdAttr).ShouldBeTrue();
        clientIdAttr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("my-client");

        loaded.ExtendedProperties.TryGet(TestIdentityProviderAttributes.TenantId.Code, out var tenantAttr).ShouldBeTrue();
        tenantAttr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("acme");
    }

    [Fact]
    public async Task update_with_extended_properties_succeeds()
    {
        var admin = _fixture.IdentityProviderAdmin;
        var provider = new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "oidc"
        };

        var createResult = await admin.CreateAsync(provider, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item.ToUpdate();
        toUpdate.ExtendedProperties.Set(TestIdentityProviderAttributes.TenantId, "updated-tenant");

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item.ExtendedProperties.TryGet(TestIdentityProviderAttributes.TenantId.Code, out var attr).ShouldBeTrue();
        attr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("updated-tenant");
    }

    [Fact]
    public async Task all_oidc_attributes_round_trip_with_typed_accessors()
    {
        var admin = _fixture.IdentityProviderAdmin;
        var store = _fixture.IdentityProviderStore;

        var scheme = $"provider_{Guid.NewGuid():N}";
        var provider = new CreateIdentityProvider
        {
            Scheme = scheme,
            Type = "oidc"
        };
        provider.ExtendedProperties.Set(AttributeCode.Create("Authority"), "https://idp.example.com");
        provider.ExtendedProperties.Set(AttributeCode.Create("ClientId"), "my-client");
        provider.ExtendedProperties.Set(AttributeCode.Create("ClientSecret"), "super-secret");
        provider.ExtendedProperties.Set(AttributeCode.Create("ResponseType"), "code");
        provider.ExtendedProperties.Set(AttributeCode.Create("Scope"), "openid profile email");
        provider.ExtendedProperties.Set(AttributeCode.Create("GetClaimsFromUserInfoEndpoint"), "false");
        provider.ExtendedProperties.Set(AttributeCode.Create("UsePkce"), "true");
        provider.ExtendedProperties.Set(TestIdentityProviderAttributes.TenantId, "acme");

        var createResult = await admin.CreateAsync(provider, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        // Verify admin round-trip
        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item.ExtendedProperties.Count.ShouldBe(8);

        // Verify store produces correct OidcProvider typed accessors
        var loaded = await store.GetBySchemeAsync(scheme, _ct);
        loaded.ShouldNotBeNull();
        var oidc = loaded.ShouldBeOfType<Models.OidcProvider>();
        oidc.Authority.ShouldBe("https://idp.example.com");
        oidc.ClientId.ShouldBe("my-client");
        oidc.ClientSecret.ShouldBe("super-secret");
        oidc.ResponseType.ShouldBe("code");
        oidc.Scope.ShouldBe("openid profile email");
        oidc.GetClaimsFromUserInfoEndpoint.ShouldBeFalse();
        oidc.UsePkce.ShouldBeTrue();
    }

    [Fact]
    public async Task oidc_provider_boolean_string_values_map_to_typed_accessors()
    {
        var admin = _fixture.IdentityProviderAdmin;
        var store = _fixture.IdentityProviderStore;

        var scheme = $"provider_{Guid.NewGuid():N}";
        var provider = new CreateIdentityProvider
        {
            Scheme = scheme,
            Type = "oidc"
        };
        provider.ExtendedProperties.Set(AttributeCode.Create("Authority"), "https://idp.example.com");
        provider.ExtendedProperties.Set(AttributeCode.Create("ClientId"), "client");
        provider.ExtendedProperties.Set(AttributeCode.Create("GetClaimsFromUserInfoEndpoint"), "false");
        provider.ExtendedProperties.Set(AttributeCode.Create("UsePkce"), "false");

        var createResult = await admin.CreateAsync(provider, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var loaded = await store.GetBySchemeAsync(scheme, _ct);
        var oidc = loaded.ShouldBeOfType<Models.OidcProvider>();
        oidc.GetClaimsFromUserInfoEndpoint.ShouldBeFalse();
        oidc.UsePkce.ShouldBeFalse();
    }

    // ── SAML provider round-trip ───────────────────────────────────────────

    [Fact]
    public async Task saml_provider_extended_properties_round_trip()
    {
        var admin = _fixture.IdentityProviderAdmin;

        var provider = new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "saml"
        };
        provider.ExtendedProperties.Set(AttributeCode.Create("IdpEntityId"), "https://idp.example.com/saml");
        provider.ExtendedProperties.Set(AttributeCode.Create("SingleSignOnServiceUrl"), "https://idp.example.com/sso");
        provider.ExtendedProperties.Set(AttributeCode.Create("BindingType"), "post");
        provider.ExtendedProperties.Set(AttributeCode.Create("AllowUnsolicitedAuthnResponse"), "true");
        provider.ExtendedProperties.Set(AttributeCode.Create("WantAssertionsSigned"), "false");

        var createResult = await admin.CreateAsync(provider, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item.ExtendedProperties.Count.ShouldBe(5);

        getResult.Item.ExtendedProperties.TryGet(AttributeCode.Create("IdpEntityId"), out var entityAttr).ShouldBeTrue();
        entityAttr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("https://idp.example.com/saml");

        getResult.Item.ExtendedProperties.TryGet(AttributeCode.Create("BindingType"), out var bindingAttr).ShouldBeTrue();
        bindingAttr.ShouldBeOfType<AttributeValue<string>>().TypedValue.ShouldBe("post");
    }

    [Fact]
    public async Task saml_provider_store_populates_properties_from_extended_values()
    {
        var admin = _fixture.IdentityProviderAdmin;
        var store = _fixture.IdentityProviderStore;

        var scheme = $"provider_{Guid.NewGuid():N}";
        var provider = new CreateIdentityProvider
        {
            Scheme = scheme,
            Type = "saml"
        };
        provider.ExtendedProperties.Set(AttributeCode.Create("IdpEntityId"), "https://idp.example.com/saml");
        provider.ExtendedProperties.Set(AttributeCode.Create("SingleSignOnServiceUrl"), "https://idp.example.com/sso");
        provider.ExtendedProperties.Set(AttributeCode.Create("AllowUnsolicitedAuthnResponse"), "true");
        provider.ExtendedProperties.Set(AttributeCode.Create("WantAssertionsSigned"), "false");

        var createResult = await admin.CreateAsync(provider, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var loaded = await store.GetBySchemeAsync(scheme, _ct);
        loaded.ShouldNotBeNull();

        // The store populates Properties from EAV - verify the dict values that
        // SamlProvider typed accessors would read. The test fixture may not register
        // the SAML dynamic provider type, so we verify via the Properties dict directly.
        loaded.Properties.ShouldContainKeyAndValue("IdpEntityId", "https://idp.example.com/saml");
        loaded.Properties.ShouldContainKeyAndValue("SingleSignOnServiceUrl", "https://idp.example.com/sso");
        loaded.Properties.ShouldContainKeyAndValue("AllowUnsolicitedAuthnResponse", "true");
        loaded.Properties.ShouldContainKeyAndValue("WantAssertionsSigned", "false");

        // Verify that constructing a SamlProvider from the base model yields correct typed accessors
        var saml = new Models.SamlProvider(loaded);
        saml.IdpEntityId.ShouldBe("https://idp.example.com/saml");
        saml.SingleSignOnServiceUrl.ShouldBe("https://idp.example.com/sso");
        saml.AllowUnsolicitedAuthnResponse.ShouldBeTrue();
        saml.WantAssertionsSigned.ShouldBeFalse();
    }

    [Fact]
    public async Task saml_provider_rejects_unknown_attributes()
    {
        var admin = _fixture.IdentityProviderAdmin;

        var provider = new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "saml"
        };
        provider.ExtendedProperties.Set(AttributeCode.Create("Authority"), "https://wrong-schema-attr");

        var result = await admin.CreateAsync(provider, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}
