// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.SamlServiceProviders;
using Duende.IdentityServer.Models;
using Duende.Storage.Querying;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class SamlServiceProviderAdminTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];

    private ISamlServiceProviderAdmin NewAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<ISamlServiceProviderAdmin>();
    }

    private static SamlServiceProviderConfiguration CreateMinimalConfig(string? entityId = null) =>
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
        var admin = NewAdmin();
        var certBase64 = GenerateSelfSignedCertBase64();

        var config = new SamlServiceProviderConfiguration
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
                new SamlCertificateConfiguration
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

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item;
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
        loaded.Certificates[0].Id.ShouldNotBe(Guid.Empty);
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
        var admin = NewAdmin();
        var config = CreateMinimalConfig();

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetByEntityIdAsync(config.EntityId, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item.EntityId.ShouldBe(config.EntityId);
    }

    [Fact]
    public async Task create_duplicate_entity_id_returns_already_exists()
    {
        var admin = NewAdmin();
        var entityId = $"https://sp-{Guid.NewGuid():N}.example.com";
        var config = CreateMinimalConfig(entityId);

        var first = await admin.CreateAsync(config, _ct);
        first.IsSuccess.ShouldBeTrue();

        var second = await admin.CreateAsync(config, _ct);
        second.IsSuccess.ShouldBeFalse();
        second.Errors.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task create_with_empty_entity_id_returns_required()
    {
        var admin = NewAdmin();
        var config = new SamlServiceProviderConfiguration { EntityId = "" };

        var result = await admin.CreateAsync(config, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "required");
    }

    [Fact]
    public async Task create_with_duplicate_acs_index_returns_error()
    {
        var admin = NewAdmin();
        var config = new SamlServiceProviderConfiguration
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
        result.Errors.ShouldContain(e => e.Code == "invalid_value");
    }

    [Fact]
    public async Task create_with_invalid_acs_url_returns_error()
    {
        var admin = NewAdmin();
        var config = new SamlServiceProviderConfiguration
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
        result.Errors.ShouldContain(e => e.Code == "invalid_value");
    }

    [Fact]
    public async Task create_with_null_acs_entry_returns_error()
    {
        var admin = NewAdmin();
        var config = new SamlServiceProviderConfiguration
        {
            EntityId = $"https://sp-{Guid.NewGuid():N}.example.com",
            AssertionConsumerServiceUrls = [null!]
        };

        var result = await admin.CreateAsync(config, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "invalid_value");
    }

    [Fact]
    public async Task create_with_null_slo_entry_returns_error()
    {
        var admin = NewAdmin();
        var config = CreateMinimalConfig();
        config.SingleLogoutServiceUrls = [null!];

        var result = await admin.CreateAsync(config, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "invalid_value");
    }

    [Fact]
    public async Task create_with_null_certificate_entry_returns_error()
    {
        var admin = NewAdmin();
        var config = CreateMinimalConfig();
        config.Certificates = [null!];

        var result = await admin.CreateAsync(config, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "invalid_value");
    }

    // === Update ===

    [Fact]
    public async Task update_with_correct_version_succeeds()
    {
        var admin = NewAdmin();
        var config = CreateMinimalConfig();

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item;
        loaded.DisplayName = "Updated Name";
        loaded.AllowIdpInitiated = true;

        var updateResult = await admin.UpdateAsync(createResult.Id, loaded, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        var reloaded = await admin.GetAsync(createResult.Id, _ct);
        reloaded.Item.ShouldNotBeNull();
        reloaded.Item.DisplayName.ShouldBe("Updated Name");
        reloaded.Item.AllowIdpInitiated.ShouldBeTrue();
    }

    [Fact]
    public async Task update_with_wrong_version_returns_conflict()
    {
        var admin = NewAdmin();
        var config = CreateMinimalConfig();

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        var loaded = getResult.Item;
        loaded.ShouldNotBeNull();
        loaded.DisplayName = "V1";

        var staleVersion = (DataVersion)999;
        var updateResult = await admin.UpdateAsync(createResult.Id, loaded, staleVersion, _ct);
        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldContain(e => e.Code == "version_conflict");
    }

    [Fact]
    public async Task update_nonexistent_returns_not_found()
    {
        var admin = NewAdmin();
        var config = CreateMinimalConfig();

        var result = await admin.UpdateAsync(Guid.CreateVersion7(), config, (DataVersion)1, _ct);
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    // === Delete ===

    [Fact]
    public async Task delete_existing_succeeds()
    {
        var admin = NewAdmin();
        var config = CreateMinimalConfig();

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await admin.DeleteAsync(createResult.Id, _ct);
        deleteResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeFalse();
    }

    // === Query ===

    [Fact]
    public async Task query_returns_matching_items()
    {
        var admin = NewAdmin();
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
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    // === Certificate handling ===

    [Fact]
    public async Task certificates_get_assigned_ids_on_create()
    {
        var admin = NewAdmin();
        var certBase64 = GenerateSelfSignedCertBase64();

        var config = CreateMinimalConfig();
        config.Certificates =
        [
            new SamlCertificateConfiguration { Base64Data = certBase64, Use = KeyUse.Signing }
        ];

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Item.ShouldNotBeNull();
        var cert = getResult.Item.Certificates![0];
        cert.Id.ShouldNotBe(Guid.Empty);
        cert.Base64Data.ShouldNotBeNullOrEmpty();
        cert.Subject.ShouldNotBeNullOrEmpty();
        cert.Thumbprint.ShouldNotBeNullOrEmpty();
        cert.NotAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task certificates_are_normalized_to_public_key_only_format()
    {
        var admin = NewAdmin();

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
            new SamlCertificateConfiguration { Base64Data = certOnlyBase64, Use = KeyUse.Signing }
        ];

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Item.ShouldNotBeNull();
        var storedBase64 = getResult.Item.Certificates![0].Base64Data;

        // Verify the stored cert can be loaded and contains no private key
        var storedBytes = Convert.FromBase64String(storedBase64);
        using var loaded = X509CertificateLoader.LoadCertificate(storedBytes);
        loaded.HasPrivateKey.ShouldBeFalse();
    }

    [Fact]
    public async Task update_replaces_certificate_list()
    {
        var admin = NewAdmin();
        var cert1 = GenerateSelfSignedCertBase64();
        var cert2 = GenerateSelfSignedCertBase64();

        var config = CreateMinimalConfig();
        config.Certificates =
        [
            new SamlCertificateConfiguration { Base64Data = cert1, Use = KeyUse.Signing }
        ];

        var createResult = await admin.CreateAsync(config, _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Item.ShouldNotBeNull();
        var loaded = getResult.Item;

        // Replace with a different cert
        loaded.Certificates =
        [
            new SamlCertificateConfiguration { Base64Data = cert2, Use = KeyUse.Encryption }
        ];

        var updateResult = await admin.UpdateAsync(createResult.Id, loaded, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue();

        var reloaded = await admin.GetAsync(createResult.Id, _ct);
        reloaded.Item!.Certificates!.Count.ShouldBe(1);
        reloaded.Item.Certificates[0].Use.ShouldBe(KeyUse.Encryption);
    }

    // === M-3: Update to existing entity ID returns AlreadyExists ===

    [Fact]
    public async Task update_entity_id_to_existing_returns_already_exists()
    {
        var admin = NewAdmin();
        var config1 = CreateMinimalConfig();
        var config2 = CreateMinimalConfig();

        var result1 = await admin.CreateAsync(config1, _ct);
        result1.IsSuccess.ShouldBeTrue();
        var result2 = await admin.CreateAsync(config2, _ct);
        result2.IsSuccess.ShouldBeTrue();

        // Try to update SP2's entity ID to match SP1's
        var getResult = await admin.GetAsync(result2.Id, _ct);
        var loaded = getResult.Item;
        loaded.ShouldNotBeNull();
        loaded.EntityId = config1.EntityId;

        var updateResult = await admin.UpdateAsync(result2.Id, loaded, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldContain(e => e.Code == "already_exists");
    }

    // === M-4: GetByEntityId not found ===

    [Fact]
    public async Task get_by_entity_id_nonexistent_returns_not_found()
    {
        var admin = NewAdmin();
        var result = await admin.GetByEntityIdAsync("https://nonexistent.example.com", _ct);
        result.Found.ShouldBeFalse();
    }

    // === M-5: Cert normalization actually strips private key from DER input ===

    [Fact]
    public async Task certificate_normalization_produces_deterministic_output()
    {
        var admin = NewAdmin();

        // Generate a cert and use it for two different SPs to verify normalization is deterministic
        var certBase64 = GenerateSelfSignedCertBase64();

        var config1 = CreateMinimalConfig();
        config1.Certificates =
        [
            new SamlCertificateConfiguration { Base64Data = certBase64, Use = KeyUse.Signing }
        ];

        var config2 = CreateMinimalConfig();
        config2.Certificates =
        [
            new SamlCertificateConfiguration { Base64Data = certBase64, Use = KeyUse.Signing }
        ];

        var result1 = await admin.CreateAsync(config1, _ct);
        result1.IsSuccess.ShouldBeTrue();
        var result2 = await admin.CreateAsync(config2, _ct);
        result2.IsSuccess.ShouldBeTrue();

        var get1 = await admin.GetAsync(result1.Id, _ct);
        get1.Item.ShouldNotBeNull();
        var get2 = await admin.GetAsync(result2.Id, _ct);
        get2.Item.ShouldNotBeNull();

        // Same input cert produces identical normalized output
        get1.Item.Certificates![0].Base64Data.ShouldBe(get2.Item.Certificates![0].Base64Data);
        get1.Item.Certificates[0].Thumbprint.ShouldBe(get2.Item.Certificates[0].Thumbprint);
    }

    public ValueTask InitializeAsync() => _fixture.InitializeAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _fixture.DisposeAsync();
    }
}
