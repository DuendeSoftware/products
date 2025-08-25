// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using IntegrationTests.Common;
using Microsoft.AspNetCore.Builder;

namespace IdentityServer.IntegrationTests.Endpoints.OAuthMetadata;

public class OAuthMetadataTests
{
    private const string Category = "OAuth Authorization Server Metadata endpoint";

    [Fact]
    [Trait("Category", Category)]
    public async Task when_issuer_identifier_has_no_subpath_then_metadata_endpoint_is_at_well_known_location()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();

        var result = await pipeline.BackChannelClient.GetAsync("HTTPS://SERVER/.WELL-KNOWN/OAUTH-AUTHORIZATION-SERVER");

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

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/oauth-authorization-server/identity#fragment");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().ShouldBe("https://server/identity");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_issuer_has_explicitly_been_set_then_metadata_endpoint_uses_configured_issuer_for_issuer()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();
        pipeline.Options.IssuerUri = "https://example.com/explicit";

        var result = await pipeline.BackChannelClient.GetAsync("https://server/.well-known/oauth-authorization-server");

        var json = await result.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        data["issuer"].GetString().ShouldBe("https://example.com/explicit");
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
}
