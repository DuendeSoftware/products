// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Stores.Storage.Clients;

namespace UnitTests.Stores.Storage.Clients;

public class ClientStructureValidatorTests
{
    private readonly ClientStructureValidator _sut = new(new ValidationOptions());

    private static ClientConfiguration Minimal(Action<Builder>? configure = null)
    {
        var builder = new Builder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    private sealed class Builder
    {
        public string? ClientUri { get; set; }
        public string? LogoUri { get; set; }
        public string? FrontChannelLogoutUri { get; set; }
        public string? BackChannelLogoutUri { get; set; }
        public string? InitiateLoginUri { get; set; }
        public IReadOnlyList<string>? RedirectUris { get; set; }
        public IReadOnlyList<string>? PostLogoutRedirectUris { get; set; }
        public IReadOnlyList<string>? AllowedCorsOrigins { get; set; }

        public ClientConfiguration Build() => new()
        {
            ClientId = "client1",
            ClientUri = ClientUri,
            LogoUri = LogoUri,
            FrontChannelLogoutUri = FrontChannelLogoutUri,
            BackChannelLogoutUri = BackChannelLogoutUri,
            InitiateLoginUri = InitiateLoginUri,
            RedirectUris = RedirectUris ?? [],
            PostLogoutRedirectUris = PostLogoutRedirectUris ?? [],
            AllowedCorsOrigins = AllowedCorsOrigins ?? []
        };
    }

    [Fact]
    public async Task redirect_uri_with_non_loopback_http_is_rejected()
    {
        const string value = "http://app.example.com/cb";
        var configuration = Minimal(b => b.RedirectUris = [value]);

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("invalid_value");
        error.PropertyNames!.ShouldContain(nameof(ClientConfiguration.RedirectUris));
        error.Message.ShouldContain(value);
    }

    [Fact]
    public async Task post_logout_redirect_uri_with_fragment_is_rejected()
    {
        const string value = "https://app.example.com/cb#frag";
        var configuration = Minimal(b => b.PostLogoutRedirectUris = [value]);

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("invalid_value");
        error.PropertyNames!.ShouldContain(nameof(ClientConfiguration.PostLogoutRedirectUris));
        error.Message.ShouldContain(value);
    }

    [Fact]
    public async Task cors_origin_with_non_loopback_http_is_rejected()
    {
        const string value = "http://spa.example.com";
        var configuration = Minimal(b => b.AllowedCorsOrigins = [value]);

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("invalid_value");
        error.PropertyNames!.ShouldContain(nameof(ClientConfiguration.AllowedCorsOrigins));
        error.Message.ShouldContain(value);
    }

    [Fact]
    public async Task cors_origin_with_loopback_http_is_accepted()
    {
        var configuration = Minimal(b => b.AllowedCorsOrigins = ["http://localhost:3000"]);

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task cors_origin_with_path_is_rejected()
    {
        const string value = "https://spa.example.com/callback";
        var configuration = Minimal(b => b.AllowedCorsOrigins = [value]);

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("invalid_value");
        error.PropertyNames!.ShouldContain(nameof(ClientConfiguration.AllowedCorsOrigins));
        error.Message.ShouldContain(value);
    }

    [Fact]
    public async Task redirect_uri_with_whitespace_is_rejected()
    {
        const string value = "  https://app.example.com/cb";
        var configuration = Minimal(b => b.RedirectUris = [value]);

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("invalid_value");
        error.PropertyNames!.ShouldContain(nameof(ClientConfiguration.RedirectUris));
        error.Message.ShouldContain(value);
    }

    [Fact]
    public async Task redirect_uri_with_odd_loopback_form_is_rejected()
    {
        const string value = "http://2130706433/cb";
        var configuration = Minimal(b => b.RedirectUris = [value]);

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("invalid_value");
        error.PropertyNames!.ShouldContain(nameof(ClientConfiguration.RedirectUris));
        error.Message.ShouldContain(value);
    }

    [Theory]
    [InlineData(nameof(ClientConfiguration.ClientUri))]
    [InlineData(nameof(ClientConfiguration.LogoUri))]
    [InlineData(nameof(ClientConfiguration.FrontChannelLogoutUri))]
    [InlineData(nameof(ClientConfiguration.InitiateLoginUri))]
    public async Task single_web_uri_fields_reject_custom_scheme(string field)
    {
        const string value = "com.example.app:/cb";
        var configuration = Minimal(b =>
        {
            switch (field)
            {
                case nameof(ClientConfiguration.ClientUri): b.ClientUri = value; break;
                case nameof(ClientConfiguration.LogoUri): b.LogoUri = value; break;
                case nameof(ClientConfiguration.FrontChannelLogoutUri): b.FrontChannelLogoutUri = value; break;
                case nameof(ClientConfiguration.InitiateLoginUri): b.InitiateLoginUri = value; break;
            }
        });

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("invalid_value");
        error.PropertyNames!.ShouldContain(field);
        error.Message.ShouldContain(value);
    }

    [Fact]
    public async Task back_channel_logout_uri_allows_non_loopback_http()
    {
        var configuration = Minimal(b => b.BackChannelLogoutUri = "http://webapp/logout");

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task back_channel_logout_uri_rejects_custom_scheme()
    {
        const string value = "com.example.app:/logout";
        var configuration = Minimal(b => b.BackChannelLogoutUri = value);

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("invalid_value");
        error.PropertyNames!.ShouldContain(nameof(ClientConfiguration.BackChannelLogoutUri));
        error.Message.ShouldContain(value);
    }

    [Fact]
    public async Task whitespace_only_single_value_is_rejected()
    {
        var configuration = Minimal(b => b.ClientUri = "   ");

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("invalid_value");
        error.PropertyNames!.ShouldContain(nameof(ClientConfiguration.ClientUri));
        error.Message.ShouldContain("must not be empty or whitespace");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task null_or_empty_single_values_are_accepted(string? value)
    {
        var configuration = Minimal(b => b.ClientUri = value);

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        errors.ShouldBeEmpty();
    }

    // === Front-channel logout URI same-origin rule (D2) ===

    [Fact]
    public async Task front_channel_origin_matching_redirect_uri_is_accepted()
    {
        var configuration = Minimal(b =>
        {
            b.FrontChannelLogoutUri = "https://app.example.com/signout";
            b.RedirectUris = ["https://app.example.com/cb"];
        });

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task front_channel_origin_mismatch_is_rejected()
    {
        const string value = "https://other.example.com/signout";
        var configuration = Minimal(b =>
        {
            b.FrontChannelLogoutUri = value;
            b.RedirectUris = ["https://app.example.com/cb"];
        });

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("invalid_value");
        error.PropertyNames!.ShouldContain(nameof(ClientConfiguration.FrontChannelLogoutUri));
        error.Message.ShouldContain(value);
    }

    [Fact]
    public async Task front_channel_origin_check_is_skipped_when_no_redirect_uris_registered()
    {
        // Amended D2: no RedirectUris registered (e.g. PAR client with unregistered pushed
        // redirect URIs) skips the origin check entirely, so the client is accepted.
        var configuration = Minimal(b => b.FrontChannelLogoutUri = "https://app.example.com/signout");

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task front_channel_origin_matching_redirect_uri_with_different_host_encoding_is_accepted()
    {
        // Unicode host on the front-channel logout URI, punycode host on the redirect URI:
        // these are the same origin and must be accepted.
        var configuration = Minimal(b =>
        {
            b.FrontChannelLogoutUri = "https://bücher.example/signout";
            b.RedirectUris = ["https://xn--bcher-kva.example/cb"];
        });

        var errors = await _sut.ValidateAsync(configuration, TestContext.Current.CancellationToken);

        errors.ShouldBeEmpty();
    }
}
