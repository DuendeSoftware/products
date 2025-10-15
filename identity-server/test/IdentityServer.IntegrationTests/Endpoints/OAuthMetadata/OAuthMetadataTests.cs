// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.OAuthMetadata;

public class OAuthMetadataTests
{
    private const string Category = "OAuth Metadata endpoint";

    [Fact]
    [Trait("Category", Category)]
    public async Task when_request_is_not_made_with_GET_then_405_is_returned()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();

        var result = await pipeline.BackChannelClient.PostAsync("https://server/.well-known/oauth-authorization-server", null);

        result.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_issuer_identifier_has_no_subpath_then_metadata_endpoint_is_at_default_well_known_location()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/oauth-authorization-server");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().ShouldBe("https://server");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_issuer_identifier_has_subpath_then_metadata_endpoint_is_at_spec_compliant_location()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPreConfigure += app => app.UsePathBase("/identity");
        pipeline.Initialize();
        pipeline.Options.IssuerUri = "https://server/identity";

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/oauth-authorization-server/identity");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().ShouldBe("https://server/identity");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_issuer_identifier_has_subpath_and_request_for_metadata_has_query_string_then_query_string_is_ignored_when_setting_issuer()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPreConfigure += app => app.UsePathBase("/identity");
        pipeline.Initialize();
        pipeline.Options.IssuerUri = "https://server/identity";

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/oauth-authorization-server/identity?query=string");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().ShouldBe("https://server/identity");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_issuer_identifier_has_subpath_and_request_for_metadata_has_fragment_then_fragment_is_ignored_when_setting_issuer()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPreConfigure += app => app.UsePathBase("/identity");
        pipeline.Initialize();
        pipeline.Options.IssuerUri = "https://server/identity";

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/oauth-authorization-server/identity#fragment");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().ShouldBe("https://server/identity");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_issuer_has_explicitly_been_set_then_metadata_endpoint_uses_configured_value_for_issuer()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();
        pipeline.Options.IssuerUri = "https://server/explicit";

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/oauth-authorization-server/explicit");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().ShouldBe("https://server/explicit");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_issuer_has_been_explicitly_set_with_subpath_and_request_for_metadata_does_not_set_subpath_after_well_known_then_404_is_returned()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();
        pipeline.Options.IssuerUri = "https://example.com/explicit";

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/oauth-authorization-server");

        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_Map_is_used_then_endpoint_is_not_available_due_to_no_spec_compliant_location()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize("/identity");

        var result = await pipeline.BackChannelClient.GetAsync("https://server/identity/.well-known/oauth-authorization-server");

        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_issuer_identifier_has_subpath_and_request_for_metadata_uses_different_subpath_then_404_is_returned()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPreConfigure += app => app.UsePathBase("/identity");
        pipeline.Initialize();
        pipeline.Options.IssuerUri = "https://server/identity";

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/oauth-authorization-server/wrong");

        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_issuer_has_subpath_and_request_is_valid_then_used_issuers_only_tracks_one_issuer()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.OnPreConfigure += app => app.UsePathBase("/identity");
        pipeline.Initialize();
        pipeline.Options.IssuerUri = "https://server/identity";

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/oauth-authorization-server/identity");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().ShouldBe("https://server/identity");

        var licenseUsageSummary = pipeline.Server.Services.GetRequiredService<LicenseUsageSummary>();
        licenseUsageSummary.IssuersUsed.Count.ShouldBe(1);
        licenseUsageSummary.IssuersUsed.ShouldBe(["https://server/identity"]);
    }
}
