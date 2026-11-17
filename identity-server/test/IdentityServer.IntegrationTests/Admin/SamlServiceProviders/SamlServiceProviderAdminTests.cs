// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.IdentityServer.Models;
using DataVersion = Duende.IdentityServer.Models.DataVersion;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class SamlServiceProviderAdminTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static CreateSamlServiceProvider CreateMinimalConfig(string? entityId = null) =>
        new()
        {
            EntityId = entityId ?? $"https://sp-{Guid.NewGuid():N}.example.com",
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            AllowedScopes = ["openid"]
        };

    private static string GenerateSelfSignedCertBase64() =>
        SamlTestCertificates.GenerateSelfSignedCertBase64();

    // === Create ===

    [Fact]
    public async Task create_and_get_by_id_round_trips_all_fields()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var certBase64 = GenerateSelfSignedCertBase64();

        var config = new CreateSamlServiceProvider
        {
            EntityId = $"https://sp-{Guid.NewGuid():N}.example.com",
            Enabled = true,
            DisplayName = "Test SP",
            Description = "A test service provider",
            ClockSkew = TimeSpan.FromMinutes(5),
            RequestMaxAge = TimeSpan.FromMinutes(10),
            AssertionLifetime = TimeSpan.FromMinutes(15),
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                },
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs2",
                    Binding = SamlBinding.HttpPost,
                    Index = 1,
                    IsDefault = false
                }
            ],
            SingleLogoutServiceUrls =
            [
                new SamlEndpointConfiguration
                {
                    Location = "https://sp.example.com/slo",
                    Binding = SamlBinding.HttpRedirect
                }
            ],
            RequireSignedAuthnRequests = true,
            RequireSignedLogoutResponses = false,
            Certificates =
            [
                new SamlCertificateInput
                {
                    Base64Data = certBase64,
                    Use = KeyUse.Signing
                }
            ],
            AllowIdpInitiated = true,
            AllowedScopes = ["openid", "profile"],
            ClaimMappings = new Dictionary<string, string> { ["email"] = "mail" },
            AuthnContextMappings = new Dictionary<string, string> { ["pwd"] = "urn:oasis:names:tc:SAML:2.0:ac:classes:Password" },
            RequestedClaimTypes = ["email"],
            DefaultNameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            EmailNameIdClaimType = "email",
            SigningBehavior = SamlSigningBehavior.SignAssertion,
            AllowedSignatureAlgorithms = ["http://www.w3.org/2001/04/xmldsig-more#rsa-sha256"]
        };

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item!;
        loaded.EntityId.ShouldBe(config.EntityId);
        loaded.Enabled.ShouldBeTrue();
        loaded.DisplayName.ShouldBe("Test SP");
        loaded.Description.ShouldBe("A test service provider");
        loaded.ClockSkew.ShouldBe(TimeSpan.FromMinutes(5));
        loaded.RequestMaxAge.ShouldBe(TimeSpan.FromMinutes(10));
        loaded.AssertionLifetime.ShouldBe(TimeSpan.FromMinutes(15));
        loaded.AssertionConsumerServiceUrls.ShouldNotBeNull();
        loaded.AssertionConsumerServiceUrls!.Count.ShouldBe(2);
        loaded.SingleLogoutServiceUrls.ShouldNotBeNull();
        loaded.SingleLogoutServiceUrls!.Count.ShouldBe(1);
        loaded.RequireSignedAuthnRequests.ShouldBe(true);
        loaded.RequireSignedLogoutResponses.ShouldBe(false);
        loaded.Certificates.ShouldNotBeNull();
        loaded.Certificates!.Count.ShouldBe(1);
        loaded.Certificates[0].Id!.ShouldNotBe(Guid.Empty);
        loaded.Certificates[0].Subject.ShouldNotBeNullOrEmpty();
        loaded.Certificates[0].Thumbprint.ShouldNotBeNullOrEmpty();
        loaded.Certificates[0].NotAfter.ShouldNotBeNull();
        loaded.AllowIdpInitiated.ShouldBeTrue();
        loaded.AllowedScopes.ShouldBe(["openid", "profile"]);
        loaded.ClaimMappings.ShouldBe(new Dictionary<string, string> { ["email"] = "mail" });
        loaded.AuthnContextMappings.ShouldBe(new Dictionary<string, string> { ["pwd"] = "urn:oasis:names:tc:SAML:2.0:ac:classes:Password" });
        loaded.RequestedClaimTypes.ShouldBe(["email"]);
        loaded.DefaultNameIdFormat.ShouldBe("urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress");
        loaded.EmailNameIdClaimType.ShouldBe("email");
        loaded.SigningBehavior.ShouldBe(SamlSigningBehavior.SignAssertion);
        loaded.AllowedSignatureAlgorithms.ShouldBe(["http://www.w3.org/2001/04/xmldsig-more#rsa-sha256"]);
    }

    [Fact]
    public async Task create_and_get_by_entity_id_returns_same()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = CreateMinimalConfig();

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetByEntityIdAsync(config.EntityId, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.EntityId.ShouldBe(config.EntityId);
    }

    [Fact]
    public async Task create_duplicate_entity_id_returns_already_exists()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var entityId = $"https://sp-{Guid.NewGuid():N}.example.com";
        var config = CreateMinimalConfig(entityId);

        var first = await admin.CreateAsync(config, _ct);
        first.IsSuccess.ShouldBeTrue();

        var second = await admin.CreateAsync(config, _ct);
        second.IsSuccess.ShouldBeFalse();
        second.Errors!.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task create_with_empty_entity_id_returns_required()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = new CreateSamlServiceProvider { EntityId = "" };

        var result = await admin.CreateAsync(config, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "required");
    }

    [Fact]
    public async Task create_with_null_allowed_scopes_succeeds_with_no_scopes()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = CreateMinimalConfig();
        config.AllowedScopes = null!;

        var result = await admin.CreateAsync(config, _ct);
        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");

        var getResult = await admin.GetByEntityIdAsync(config.EntityId, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.AllowedScopes.ShouldBeEmpty();
    }

    [Fact]
    public async Task update_with_null_allowed_scopes_succeeds_with_no_scopes()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var createResult = await admin.CreateAsync(CreateMinimalConfig(), _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var update = new UpdateSamlServiceProvider
        {
            EntityId = (await admin.GetAsync(createResult.Id!, _ct)).Item!.EntityId,
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            AllowedScopes = null!
        };

        var updateResult = await admin.UpdateAsync(createResult.Id!, update, (DataVersion)1, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item!.AllowedScopes.ShouldBeEmpty();
    }

    [Fact]
    public async Task create_with_duplicate_acs_index_returns_error()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = new CreateSamlServiceProvider
        {
            EntityId = $"https://sp-{Guid.NewGuid():N}.example.com",
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs1",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                },
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs2",
                    Binding = SamlBinding.HttpPost,
                    Index = 0, // duplicate
                    IsDefault = false
                }
            ]
        };

        var result = await admin.CreateAsync(config, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "invalid_value");
    }

    [Fact]
    public async Task create_with_invalid_acs_url_returns_error()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = new CreateSamlServiceProvider
        {
            EntityId = $"https://sp-{Guid.NewGuid():N}.example.com",
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "not a url",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ]
        };

        var result = await admin.CreateAsync(config, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "invalid_value");
    }

    [Fact]
    public async Task create_with_null_acs_entry_returns_error()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = new CreateSamlServiceProvider
        {
            EntityId = $"https://sp-{Guid.NewGuid():N}.example.com",
            AssertionConsumerServiceUrls = [null!]
        };

        var result = await admin.CreateAsync(config, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "invalid_value");
    }

    [Fact]
    public async Task create_with_null_slo_entry_returns_error()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = CreateMinimalConfig();
        config.SingleLogoutServiceUrls = [null!];

        var result = await admin.CreateAsync(config, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "invalid_value");
    }

    [Fact]
    public async Task create_with_null_certificate_entry_returns_error()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = CreateMinimalConfig();
        config.Certificates = [null!];

        var result = await admin.CreateAsync(config, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "invalid_value");
    }

    // === Update ===

    [Fact]
    public async Task update_with_correct_version_succeeds()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = CreateMinimalConfig();

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item!.ToUpdate();
        loaded.DisplayName = "Updated Name";
        loaded.AllowIdpInitiated = true;

        var updateResult = await admin.UpdateAsync(createResult.Id!, loaded, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        var reloaded = await admin.GetAsync(createResult.Id!, _ct);
        reloaded.Item!.ShouldNotBeNull();
        reloaded.Item!.DisplayName.ShouldBe("Updated Name");
        reloaded.Item!.AllowIdpInitiated.ShouldBeTrue();
    }

    [Fact]
    public async Task update_with_wrong_version_returns_conflict()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = CreateMinimalConfig();

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        var loaded = getResult.Item!.ToUpdate();
        loaded.ShouldNotBeNull();
        loaded.DisplayName = "V1";

        var staleVersion = (DataVersion)999;
        var updateResult = await admin.UpdateAsync(createResult.Id!, loaded, staleVersion, _ct);
        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors!.ShouldContain(e => e.Code == "version_conflict");
    }

    [Fact]
    public async Task update_nonexistent_returns_not_found()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = new UpdateSamlServiceProvider
        {
            EntityId = $"https://sp-{Guid.NewGuid():N}.example.com",
            AssertionConsumerServiceUrls =
            [
                new SamlIndexedEndpointConfiguration
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            AllowedScopes = ["openid"]
        };

        var result = await admin.UpdateAsync(Guid.CreateVersion7(), config, (DataVersion)1, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors!.ShouldContain(e => e.Code == "not_found");
    }

    // === Delete ===

    [Fact]
    public async Task delete_existing_succeeds()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = CreateMinimalConfig();

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await admin.DeleteAsync(createResult.Id!, _ct);
        deleteResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeFalse();
    }

    // === Allowed scope resolution ===

    [Fact]
    public async Task create_with_unknown_scope_name_returns_validation_error()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config = CreateMinimalConfig();
        config.AllowedScopes = ["does_not_exist"];

        var result = await admin.CreateAsync(config, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "invalid_value" && e.PropertyNames != null && e.PropertyNames.Contains("AllowedScopes"));
    }

    [Fact]
    public async Task create_with_api_scope_name_returns_validation_error_saying_identity_resources_only()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var apiScopeName = $"scope_{Guid.NewGuid():N}";
        (await _fixture.ApiScopeAdmin.CreateAsync(new Duende.IdentityServer.Admin.ApiScopes.CreateApiScope { Name = apiScopeName }, _ct))
            .IsSuccess.ShouldBeTrue();

        var config = CreateMinimalConfig();
        config.AllowedScopes = [apiScopeName];

        var result = await admin.CreateAsync(config, _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "invalid_value"
            && e.PropertyNames != null && e.PropertyNames.Contains("AllowedScopes")
            && e.Message.Contains("identity resources", StringComparison.Ordinal));
    }

    [Fact]
    public async Task create_with_allowed_scope_naming_an_identity_resource_succeeds()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var identityResourceName = $"idres_{Guid.NewGuid():N}";
        (await _fixture.IdentityResourceAdmin.CreateAsync(
            new Duende.IdentityServer.Admin.IdentityResources.CreateIdentityResource { Name = identityResourceName }, _ct))
            .IsSuccess.ShouldBeTrue();

        var config = CreateMinimalConfig();
        config.AllowedScopes = [identityResourceName];

        var result = await admin.CreateAsync(config, _ct);

        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
    }

    [Fact]
    public async Task renaming_an_identity_resource_is_reflected_in_saml_sp_get_and_store()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var oldName = $"idres_{Guid.NewGuid():N}";
        var newName = $"idres_renamed_{Guid.NewGuid():N}";

        var createdResource = await _fixture.IdentityResourceAdmin.CreateAsync(
            new Duende.IdentityServer.Admin.IdentityResources.CreateIdentityResource { Name = oldName }, _ct);
        createdResource.IsSuccess.ShouldBeTrue();

        var config = CreateMinimalConfig();
        config.AllowedScopes = [oldName];
        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var resourceGet = await _fixture.IdentityResourceAdmin.GetAsync(createdResource.Id!, _ct);
        resourceGet.Found.ShouldBeTrue();
        var resourceUpdate = resourceGet.Item!.ToUpdate();
        resourceUpdate.Name = newName;
        var renameResult = await _fixture.IdentityResourceAdmin.UpdateAsync(createdResource.Id!, resourceUpdate, resourceGet.Version!, _ct);
        renameResult.IsSuccess.ShouldBeTrue($"Rename failed: {renameResult}");

        var spGet = await admin.GetAsync(createResult.Id!, _ct);
        spGet.Found.ShouldBeTrue();
        spGet.Item!.AllowedScopes.ShouldBe([newName]);

        var fromStore = await _fixture.SamlServiceProviderStore.FindByEntityIdAsync(config.EntityId, _ct);
        fromStore.ShouldNotBeNull();
        fromStore!.AllowedScopes.ShouldBe(new HashSet<string> { newName });
    }

    [Fact]
    public async Task deleting_an_identity_resource_allowed_by_a_saml_sp_is_refused_naming_the_sp()
    {
        var resourceName = $"idres_{Guid.NewGuid():N}";
        var createdResource = await _fixture.IdentityResourceAdmin.CreateAsync(
            new Duende.IdentityServer.Admin.IdentityResources.CreateIdentityResource { Name = resourceName }, _ct);
        createdResource.IsSuccess.ShouldBeTrue();

        var config = CreateMinimalConfig();
        config.AllowedScopes = [resourceName];
        var createResult = await _fixture.SamlServiceProviderAdmin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var deleteResult = await _fixture.IdentityResourceAdmin.DeleteAsync(createdResource.Id!, _ct);

        deleteResult.IsSuccess.ShouldBeFalse();
        deleteResult.Errors.ShouldNotBeNull();
        deleteResult.Errors.ShouldContain(e => e.Code == "validation_failed" && e.Message.Contains(config.EntityId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task list_reports_allowed_scope_count()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var identityResourceName1 = $"idres_{Guid.NewGuid():N}";
        var identityResourceName2 = $"idres_{Guid.NewGuid():N}";

        (await _fixture.IdentityResourceAdmin.CreateAsync(
            new Duende.IdentityServer.Admin.IdentityResources.CreateIdentityResource { Name = identityResourceName1 }, _ct))
            .IsSuccess.ShouldBeTrue();
        (await _fixture.IdentityResourceAdmin.CreateAsync(
            new Duende.IdentityServer.Admin.IdentityResources.CreateIdentityResource { Name = identityResourceName2 }, _ct))
            .IsSuccess.ShouldBeTrue();

        var config = CreateMinimalConfig();
        config.AllowedScopes = [identityResourceName1, identityResourceName2];
        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var result = await admin.QueryAsync(
            QueryRequest.Create<SamlServiceProviderFilter, SamlServiceProviderSortField>(new SamlServiceProviderFilter { EntityId = config.EntityId }), _ct);

        var listItem = result.Results.ShouldHaveSingleItem();
        listItem.AllowedScopeCount.ShouldBe(2);
    }

    // === Query ===

    [Fact]
    public async Task query_returns_matching_items()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var entityId1 = $"https://sp-{Guid.NewGuid():N}.example.com";
        var entityId2 = $"https://sp-{Guid.NewGuid():N}.example.com";

        var config1 = CreateMinimalConfig(entityId1);
        config1.DisplayName = "First SP";
        var config2 = CreateMinimalConfig(entityId2);
        config2.DisplayName = "Second SP";

        (await admin.CreateAsync(config1, _ct)).IsSuccess.ShouldBeTrue();
        (await admin.CreateAsync(config2, _ct)).IsSuccess.ShouldBeTrue();

        var request = QueryRequest.Create<SamlServiceProviderFilter, SamlServiceProviderSortField>();
        var result = await admin.QueryAsync(request, _ct);
        result.Results.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    // === Certificate handling ===

    [Fact]
    public async Task certificates_get_assigned_ids_on_create()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var certBase64 = GenerateSelfSignedCertBase64();

        var config = CreateMinimalConfig();
        config.Certificates =
        [
            new SamlCertificateInput { Base64Data = certBase64, Use = KeyUse.Signing }
        ];

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Item!.ShouldNotBeNull();
        var cert = getResult.Item!.Certificates![0];
        cert.Id!.ShouldNotBe(Guid.Empty);
        cert.Base64Data.ShouldNotBeNullOrEmpty();
        cert.Subject.ShouldNotBeNullOrEmpty();
        cert.Thumbprint.ShouldNotBeNullOrEmpty();
        cert.NotAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task certificates_are_normalized_to_public_key_only_format()
    {
        var admin = _fixture.SamlServiceProviderAdmin;

        // Generate a self-signed cert exported as DER (X509ContentType.Cert),
        // which only contains the public key. Normalization re-exports in the
        // same format, ensuring the stored value never contains private material.
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=NormalizeTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
        var certOnlyBase64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));

        var config = CreateMinimalConfig();
        config.Certificates =
        [
            new SamlCertificateInput { Base64Data = certOnlyBase64, Use = KeyUse.Signing }
        ];

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Item!.ShouldNotBeNull();
        var storedBase64 = getResult.Item!.Certificates![0].Base64Data;

        // Verify the stored cert can be loaded and contains no private key
        var storedBytes = Convert.FromBase64String(storedBase64);
        using var loaded = X509CertificateLoader.LoadCertificate(storedBytes);
        loaded.HasPrivateKey.ShouldBeFalse();
    }

    [Fact]
    public async Task update_replaces_certificate_list()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var cert1 = GenerateSelfSignedCertBase64();
        var cert2 = GenerateSelfSignedCertBase64();

        var config = CreateMinimalConfig();
        config.Certificates =
        [
            new SamlCertificateInput { Base64Data = cert1, Use = KeyUse.Signing }
        ];

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Item!.ShouldNotBeNull();
        var loaded = getResult.Item!.ToUpdate();

        // Replace with a different cert
        loaded.Certificates =
        [
            new SamlCertificateInput { Base64Data = cert2, Use = KeyUse.Encryption }
        ];

        var updateResult = await admin.UpdateAsync(createResult.Id!, loaded, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        var reloaded = await admin.GetAsync(createResult.Id!, _ct);
        reloaded.Item!.Certificates!.Count.ShouldBe(1);
        reloaded.Item!.Certificates[0].Use.ShouldBe(KeyUse.Encryption);
    }

    // === M-3: Update to existing entity ID returns AlreadyExists ===

    [Fact]
    public async Task update_entity_id_to_existing_returns_already_exists()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var config1 = CreateMinimalConfig();
        var config2 = CreateMinimalConfig();

        var result1 = await admin.CreateAsync(config1, _ct);
        result1.IsSuccess.ShouldBeTrue();
        var result2 = await admin.CreateAsync(config2, _ct);
        result2.IsSuccess.ShouldBeTrue();

        // Try to update SP2's entity ID to match SP1's
        var getResult = await admin.GetAsync(result2.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        var loaded = getResult.Item!.ToUpdate();
        loaded.ShouldNotBeNull();
        loaded.EntityId = config1.EntityId;

        var updateResult = await admin.UpdateAsync(result2.Id!, loaded, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors!.ShouldContain(e => e.Code == "already_exists");
    }

    // === M-4: GetByEntityId not found ===

    [Fact]
    public async Task get_by_entity_id_nonexistent_returns_not_found()
    {
        var admin = _fixture.SamlServiceProviderAdmin;
        var result = await admin.GetByEntityIdAsync("https://nonexistent.example.com", _ct);
        result.Found.ShouldBeFalse();
    }

    // === M-5: Cert normalization actually strips private key from DER input ===

    [Fact]
    public async Task certificate_normalization_produces_deterministic_output()
    {
        var admin = _fixture.SamlServiceProviderAdmin;

        // Generate a cert and use it for two different SPs to verify normalization is deterministic
        var certBase64 = GenerateSelfSignedCertBase64();

        var config1 = CreateMinimalConfig();
        config1.Certificates =
        [
            new SamlCertificateInput { Base64Data = certBase64, Use = KeyUse.Signing }
        ];

        var config2 = CreateMinimalConfig();
        config2.Certificates =
        [
            new SamlCertificateInput { Base64Data = certBase64, Use = KeyUse.Signing }
        ];

        var result1 = await admin.CreateAsync(config1, _ct);
        result1.IsSuccess.ShouldBeTrue();
        var result2 = await admin.CreateAsync(config2, _ct);
        result2.IsSuccess.ShouldBeTrue();

        var get1 = await admin.GetAsync(result1.Id!, _ct);
        get1.Item!.ShouldNotBeNull();
        var get2 = await admin.GetAsync(result2.Id!, _ct);
        get2.Item!.ShouldNotBeNull();

        // Same input cert produces identical normalized output
        get1.Item!.Certificates![0].Base64Data.ShouldBe(get2.Item!.Certificates![0].Base64Data);
        get1.Item!.Certificates[0].Thumbprint.ShouldBe(get2.Item!.Certificates[0].Thumbprint);
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.InitializeAsync();
        await _fixture.EnsureIdentityResourcesAsync("openid", "profile");
    }

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}
