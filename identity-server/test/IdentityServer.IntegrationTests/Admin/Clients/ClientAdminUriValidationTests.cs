// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

public sealed class ClientAdminUriValidationTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static void AssertInvalid(SaveResult<ClientId> result, string field, string value)
    {
        result.IsSuccess.ShouldBeFalse($"expected failure but got: {result}");
        result.Errors.ShouldNotBeNull();
        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("invalid_value");
        error.PropertyNames.ShouldNotBeNull();
        error.PropertyNames.ShouldContain(field);
        error.Message.ShouldContain(value);
    }

    private static CreateClient BaseClient(string field, string value) => field switch
    {
        nameof(CreateClient.RedirectUris) => new CreateClient { ClientId = NewClientId(), RedirectUris = [value] },
        nameof(CreateClient.PostLogoutRedirectUris) => new CreateClient { ClientId = NewClientId(), PostLogoutRedirectUris = [value] },
        nameof(CreateClient.AllowedCorsOrigins) => new CreateClient { ClientId = NewClientId(), AllowedCorsOrigins = [value] },
        nameof(CreateClient.ClientUri) => new CreateClient { ClientId = NewClientId(), ClientUri = value },
        nameof(CreateClient.LogoUri) => new CreateClient { ClientId = NewClientId(), LogoUri = value },
        nameof(CreateClient.InitiateLoginUri) => new CreateClient { ClientId = NewClientId(), InitiateLoginUri = value },
        nameof(CreateClient.FrontChannelLogoutUri) => new CreateClient { ClientId = NewClientId(), FrontChannelLogoutUri = value },
        nameof(CreateClient.BackChannelLogoutUri) => new CreateClient { ClientId = NewClientId(), BackChannelLogoutUri = value },
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field")
    };

    private static void ApplyField(UpdateClient update, string field, string value)
    {
        switch (field)
        {
            case nameof(UpdateClient.RedirectUris):
                update.RedirectUris = [value];
                break;
            case nameof(UpdateClient.PostLogoutRedirectUris):
                update.PostLogoutRedirectUris = [value];
                break;
            case nameof(UpdateClient.AllowedCorsOrigins):
                update.AllowedCorsOrigins = [value];
                break;
            case nameof(UpdateClient.ClientUri):
                update.ClientUri = value;
                break;
            case nameof(UpdateClient.LogoUri):
                update.LogoUri = value;
                break;
            case nameof(UpdateClient.InitiateLoginUri):
                update.InitiateLoginUri = value;
                break;
            case nameof(UpdateClient.FrontChannelLogoutUri):
                update.FrontChannelLogoutUri = value;
                break;
            case nameof(UpdateClient.BackChannelLogoutUri):
                update.BackChannelLogoutUri = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field");
        }
    }

    private static string NewClientId() => $"client_{Guid.NewGuid():N}";

    private async Task AssertCreateRejected(string field, string value)
    {
        var admin = _fixture.ClientAdmin;
        var client = BaseClient(field, value);

        var result = await admin.CreateAsync(client, _ct);

        AssertInvalid(result, field, value);
    }

    private async Task AssertCreateAccepted(string field, string value)
    {
        var admin = _fixture.ClientAdmin;
        var client = BaseClient(field, value);

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
    }

    private static object? GetFieldValue(ClientConfiguration configuration, string field) => field switch
    {
        nameof(ClientConfiguration.RedirectUris) => configuration.RedirectUris,
        nameof(ClientConfiguration.PostLogoutRedirectUris) => configuration.PostLogoutRedirectUris,
        nameof(ClientConfiguration.AllowedCorsOrigins) => configuration.AllowedCorsOrigins,
        nameof(ClientConfiguration.ClientUri) => configuration.ClientUri,
        nameof(ClientConfiguration.LogoUri) => configuration.LogoUri,
        nameof(ClientConfiguration.InitiateLoginUri) => configuration.InitiateLoginUri,
        nameof(ClientConfiguration.FrontChannelLogoutUri) => configuration.FrontChannelLogoutUri,
        nameof(ClientConfiguration.BackChannelLogoutUri) => configuration.BackChannelLogoutUri,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field")
    };

    private async Task AssertUpdateRejected(string field, string value)
    {
        var admin = _fixture.ClientAdmin;
        var createResult = await admin.CreateAsync(new CreateClient { ClientId = NewClientId() }, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();
        var before = getResult.Item!;
        var originalValue = GetFieldValue(before, field);

        var toUpdate = before.ToUpdate();
        ApplyField(toUpdate, field, value);

        var updateResult = await admin.UpdateAsync(createResult.Id!, toUpdate, getResult.Version!, _ct);

        AssertInvalid(updateResult, field, value);

        var after = await admin.GetAsync(createResult.Id!, _ct);
        after.Found.ShouldBeTrue();
        // The bad update must not have been applied; the field still has its original (empty/default) value.
        GetFieldValue(after.Item!, field).ShouldBe(originalValue);
    }

    private async Task AssertUpdateAccepted(string field, string value)
    {
        var admin = _fixture.ClientAdmin;
        var createResult = await admin.CreateAsync(new CreateClient { ClientId = NewClientId() }, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetAsync(createResult.Id!, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item!.ToUpdate();
        ApplyField(toUpdate, field, value);

        var updateResult = await admin.UpdateAsync(createResult.Id!, toUpdate, getResult.Version!, _ct);

        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");
    }

    // --- Accepted redirect-style values (RedirectUris / PostLogoutRedirectUris) ---

    [Theory]
    [InlineData(nameof(CreateClient.RedirectUris))]
    [InlineData(nameof(CreateClient.PostLogoutRedirectUris))]
    public async Task accepted_https_redirect_value(string field)
    {
        const string value = "https://app.example.com/cb";
        await AssertCreateAccepted(field, value);
        await AssertUpdateAccepted(field, value);
    }

    [Theory]
    [InlineData(nameof(CreateClient.RedirectUris))]
    [InlineData(nameof(CreateClient.PostLogoutRedirectUris))]
    public async Task accepted_http_localhost_redirect_value(string field)
    {
        const string value = "http://localhost:5000/cb";
        await AssertCreateAccepted(field, value);
        await AssertUpdateAccepted(field, value);
    }

    [Theory]
    [InlineData(nameof(CreateClient.RedirectUris))]
    [InlineData(nameof(CreateClient.PostLogoutRedirectUris))]
    public async Task accepted_http_loopback_ipv4_redirect_value(string field)
    {
        const string value = "http://127.0.0.1:5000/cb";
        await AssertCreateAccepted(field, value);
        await AssertUpdateAccepted(field, value);
    }

    [Theory]
    [InlineData(nameof(CreateClient.RedirectUris))]
    [InlineData(nameof(CreateClient.PostLogoutRedirectUris))]
    public async Task accepted_http_loopback_ipv6_redirect_value(string field)
    {
        const string value = "http://[::1]:5000/cb";
        await AssertCreateAccepted(field, value);
        await AssertUpdateAccepted(field, value);
    }

    [Theory]
    [InlineData(nameof(CreateClient.RedirectUris))]
    [InlineData(nameof(CreateClient.PostLogoutRedirectUris))]
    public async Task accepted_native_custom_scheme_redirect_value(string field)
    {
        const string value = "com.example.app:/cb";
        await AssertCreateAccepted(field, value);
        await AssertUpdateAccepted(field, value);
    }

    // --- Rejected: custom scheme on web-URL fields ---

    [Theory]
    [InlineData(nameof(CreateClient.ClientUri))]
    [InlineData(nameof(CreateClient.LogoUri))]
    [InlineData(nameof(CreateClient.InitiateLoginUri))]
    [InlineData(nameof(CreateClient.FrontChannelLogoutUri))]
    public async Task rejected_native_custom_scheme_on_web_url_fields(string field)
    {
        const string value = "com.example.app:/cb";
        await AssertCreateRejected(field, value);
        await AssertUpdateRejected(field, value);
    }

    // --- Rejected: non-loopback http on redirect + web URL fields ---

    [Theory]
    [InlineData(nameof(CreateClient.RedirectUris))]
    [InlineData(nameof(CreateClient.PostLogoutRedirectUris))]
    [InlineData(nameof(CreateClient.ClientUri))]
    [InlineData(nameof(CreateClient.LogoUri))]
    [InlineData(nameof(CreateClient.InitiateLoginUri))]
    public async Task rejected_non_loopback_http(string field)
    {
        const string value = "http://app.example.com/cb";
        await AssertCreateRejected(field, value);
        await AssertUpdateRejected(field, value);
    }

    [Theory]
    [InlineData(nameof(CreateClient.RedirectUris))]
    [InlineData(nameof(CreateClient.PostLogoutRedirectUris))]
    [InlineData(nameof(CreateClient.ClientUri))]
    [InlineData(nameof(CreateClient.LogoUri))]
    [InlineData(nameof(CreateClient.InitiateLoginUri))]
    public async Task rejected_non_loopback_http_uppercase_scheme_and_host(string field)
    {
        const string value = "HTTP://APP.EXAMPLE.COM/cb";
        await AssertCreateRejected(field, value);
        await AssertUpdateRejected(field, value);
    }

    // --- Rejected: relative / fragment values across every URI field ---

    [Theory]
    [InlineData(nameof(CreateClient.RedirectUris))]
    [InlineData(nameof(CreateClient.PostLogoutRedirectUris))]
    [InlineData(nameof(CreateClient.ClientUri))]
    [InlineData(nameof(CreateClient.LogoUri))]
    [InlineData(nameof(CreateClient.InitiateLoginUri))]
    [InlineData(nameof(CreateClient.BackChannelLogoutUri))]
    [InlineData(nameof(CreateClient.AllowedCorsOrigins))]
    public async Task rejected_relative_value_cb(string field)
    {
        const string value = "cb";
        await AssertCreateRejected(field, value);
        await AssertUpdateRejected(field, value);
    }

    [Theory]
    [InlineData(nameof(CreateClient.RedirectUris))]
    [InlineData(nameof(CreateClient.PostLogoutRedirectUris))]
    [InlineData(nameof(CreateClient.ClientUri))]
    [InlineData(nameof(CreateClient.LogoUri))]
    [InlineData(nameof(CreateClient.InitiateLoginUri))]
    [InlineData(nameof(CreateClient.BackChannelLogoutUri))]
    [InlineData(nameof(CreateClient.AllowedCorsOrigins))]
    public async Task rejected_relative_value_slash_cb(string field)
    {
        const string value = "/cb";
        await AssertCreateRejected(field, value);
        await AssertUpdateRejected(field, value);
    }

    [Theory]
    [InlineData(nameof(CreateClient.RedirectUris))]
    [InlineData(nameof(CreateClient.PostLogoutRedirectUris))]
    [InlineData(nameof(CreateClient.ClientUri))]
    [InlineData(nameof(CreateClient.LogoUri))]
    [InlineData(nameof(CreateClient.InitiateLoginUri))]
    [InlineData(nameof(CreateClient.BackChannelLogoutUri))]
    [InlineData(nameof(CreateClient.AllowedCorsOrigins))]
    public async Task rejected_fragment(string field)
    {
        const string value = "https://app.example.com/cb#x";
        await AssertCreateRejected(field, value);
        await AssertUpdateRejected(field, value);
    }

    // --- Rejected: deny-listed schemes on redirect-style fields ---

    [Theory]
    [InlineData(nameof(CreateClient.RedirectUris))]
    [InlineData(nameof(CreateClient.PostLogoutRedirectUris))]
    public async Task rejected_javascript_scheme(string field)
    {
        const string value = "JAVASCRIPT:alert(1)";
        await AssertCreateRejected(field, value);
        await AssertUpdateRejected(field, value);
    }

    // --- D1: BackChannelLogoutUri allows non-loopback http ---

    [Fact]
    public async Task d1_back_channel_logout_uri_http_non_loopback_is_accepted()
    {
        const string value = "http://webapp/logout";
        await AssertCreateAccepted(nameof(CreateClient.BackChannelLogoutUri), value);
        await AssertUpdateAccepted(nameof(CreateClient.BackChannelLogoutUri), value);
    }

    // --- D2: FrontChannelLogoutUri must share origin with a registered redirect URI ---

    [Fact]
    public async Task d2_front_channel_logout_uri_cross_origin_with_redirect_uris_is_rejected()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient
        {
            ClientId = NewClientId(),
            RedirectUris = ["https://app.example.com/cb"],
            FrontChannelLogoutUri = "https://other.example.com/signout"
        };

        var result = await admin.CreateAsync(client, _ct);

        AssertInvalid(result, nameof(CreateClient.FrontChannelLogoutUri), "https://other.example.com/signout");
    }

    [Fact]
    public async Task d2_front_channel_logout_uri_same_origin_with_redirect_uris_is_accepted()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient
        {
            ClientId = NewClientId(),
            RedirectUris = ["https://app.example.com/cb"],
            FrontChannelLogoutUri = "https://app.example.com/signout"
        };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
    }

    [Fact]
    public async Task d2_front_channel_logout_uri_with_no_redirect_uris_skips_origin_check()
    {
        // No redirect URIs registered: the origin check is skipped (e.g. PAR clients
        // using unregistered pushed redirect URIs). The default test fixture uses
        // NopClientConfigurationValidator, so DefaultClientConfigurationValidator's
        // "No redirect URI configured" rule does not run here.
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient
        {
            ClientId = NewClientId(),
            FrontChannelLogoutUri = "https://other.example.com/signout"
        };

        var result = await admin.CreateAsync(client, _ct);

        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
    }

    // --- D3: AllowedCorsOrigins requires https or loopback http ---

    [Fact]
    public async Task d3_cors_origin_non_loopback_http_is_rejected()
    {
        const string value = "http://spa.example.com";
        await AssertCreateRejected(nameof(CreateClient.AllowedCorsOrigins), value);
        await AssertUpdateRejected(nameof(CreateClient.AllowedCorsOrigins), value);
    }

    [Fact]
    public async Task d3_cors_origin_loopback_http_is_accepted()
    {
        const string value = "http://localhost:3000";
        await AssertCreateAccepted(nameof(CreateClient.AllowedCorsOrigins), value);
        await AssertUpdateAccepted(nameof(CreateClient.AllowedCorsOrigins), value);
    }

    // --- Deny-list enforced in the Admin API even with the extension point replaced by Nop ---

    [Fact]
    public async Task deny_list_is_enforced_even_with_nop_client_configuration_validator()
    {
        await using var provider = await StorageTestFixture.CreateProviderAsync(
            builder => builder.AddClientConfigurationValidator<NopClientConfigurationValidator>(), _ct);

        using var scope = provider.CreateScope();

        // Confirm the extension point really was replaced by the Nop validator.
        var resolvedValidator = scope.ServiceProvider.GetRequiredService<IClientConfigurationValidator>();
        resolvedValidator.ShouldBeOfType<NopClientConfigurationValidator>();

        var admin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();
        var client = new CreateClient
        {
            ClientId = NewClientId(),
            RedirectUris = ["javascript:alert(1)"]
        };

        var result = await admin.CreateAsync(client, _ct);

        AssertInvalid(result, nameof(CreateClient.RedirectUris), "javascript:alert(1)");
    }

    [Fact]
    public async Task deny_list_honors_custom_invalid_redirect_uri_prefix()
    {
        await using var provider = await StorageTestFixture.CreateProviderAsync(
            builder => builder.AddClientConfigurationValidator<NopClientConfigurationValidator>(), _ct);

        using var scope = provider.CreateScope();

        // ClientAdmin reads IdentityServerOptions.Validation live at validation time, so
        // mutating the resolved singleton instance affects the validator it constructs.
        var options = scope.ServiceProvider.GetRequiredService<IdentityServerOptions>();
        options.Validation.InvalidRedirectUriPrefixes.Add("custom:");

        var admin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();
        var client = new CreateClient
        {
            ClientId = NewClientId(),
            RedirectUris = ["custom:/cb"]
        };

        var result = await admin.CreateAsync(client, _ct);

        AssertInvalid(result, nameof(CreateClient.RedirectUris), "custom:/cb");
    }

    // --- CORS origin shape: path, query, trailing slash and userinfo are rejected ---

    [Fact]
    public async Task cors_origin_with_path_is_rejected()
    {
        const string value = "https://spa.example.com/callback";
        await AssertCreateRejected(nameof(CreateClient.AllowedCorsOrigins), value);
        await AssertUpdateRejected(nameof(CreateClient.AllowedCorsOrigins), value);
    }

    [Fact]
    public async Task cors_origin_with_default_port_is_rejected()
    {
        const string value = "https://site.test:443";
        await AssertCreateRejected(nameof(CreateClient.AllowedCorsOrigins), value);
    }

    [Fact]
    public async Task cors_origin_with_trailing_dot_segment_is_rejected()
    {
        const string value = "https://site.test/.";
        await AssertCreateRejected(nameof(CreateClient.AllowedCorsOrigins), value);
    }

    // --- Whitespace-padded redirect URI is rejected ---

    [Fact]
    public async Task whitespace_padded_redirect_uri_is_rejected()
    {
        const string value = "  https://app.example.com/cb";
        await AssertCreateRejected(nameof(CreateClient.RedirectUris), value);
        await AssertUpdateRejected(nameof(CreateClient.RedirectUris), value);
    }

    // --- Odd loopback form (decimal IPv4 host) redirect URI is rejected ---

    [Fact]
    public async Task odd_loopback_form_redirect_uri_is_rejected()
    {
        const string value = "http://2130706433/cb";
        await AssertCreateRejected(nameof(CreateClient.RedirectUris), value);
        await AssertUpdateRejected(nameof(CreateClient.RedirectUris), value);
    }

    // --- Secret re-validation smoke test ---

    [Fact]
    public async Task create_secret_on_valid_client_with_loopback_redirect_uri_succeeds()
    {
        var admin = _fixture.ClientAdmin;
        var client = new CreateClient
        {
            ClientId = NewClientId(),
            RedirectUris = ["http://localhost/cb"]
        };

        var createResult = await admin.CreateAsync(client, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var secretResult = await admin.CreateSecretAsync(
            createResult.Id!,
            new CreateClientSecret { PlaintextValue = "secret-value" },
            _ct);

        secretResult.IsSuccess.ShouldBeTrue($"CreateSecret failed: {secretResult}");
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}
