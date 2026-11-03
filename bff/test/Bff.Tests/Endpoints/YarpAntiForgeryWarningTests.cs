// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Yarp.ReverseProxy.Configuration;

namespace Duende.Bff.Tests.Endpoints;

/// <summary>
/// Tests for the non-breaking warning logged by <c>AccessTokenTransformProvider</c> when a YARP
/// route forwards a user access token without any anti-forgery (CSRF) protection.
/// See https://github.com/DuendeSoftware/products-private/issues/3885.
/// </summary>
public class YarpAntiForgeryWarningTests : BffTestBase
{
    private const string WarningCategory = "Duende.Bff.Yarp.Internal.AccessTokenTransformProvider";

    private void ConfigureYarp(RouteConfig routeConfig, ClusterConfig? clusterConfig = null) =>
        Bff.OnConfigureBff += bff =>
        {
            _ = bff.AddYarpConfig([routeConfig], [clusterConfig ?? Some.ClusterConfig(Api)]);
        };

    private List<string> WarningLogEntries => Bff.Logger.LogEntries
        .Where(e => e.Contains("[Warning]") && e.Contains(WarningCategory))
        .ToList();

    [Theory]
    [InlineData(RequiredTokenType.User)]
    [InlineData(RequiredTokenType.UserOrNone)]
    [InlineData(RequiredTokenType.UserOrClient)]
    public async Task plain_reverse_proxy_route_with_user_token_and_no_antiforgery_protection_warns_once(RequiredTokenType tokenType)
    {
        Bff.OnConfigureApp += app => _ = app.MapReverseProxy();
        ConfigureYarp(Some.RouteConfig().WithAccessToken(tokenType));

        await ConfigureBff(BffSetupType.V4Bff);
        _ = await Bff.BrowserClient.Login();

        // Call multiple times - the warning should only be logged once (per built transform).
        _ = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);
        _ = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);
        _ = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);

        var warnings = WarningLogEntries;
        warnings.Count.ShouldBe(1);
        warnings[0].ShouldContain(The.RouteId.ToString());
        warnings[0].ShouldContain("without anti-forgery protection");
    }

    [Fact]
    public async Task reverse_proxy_route_with_antiforgery_check_and_middleware_enabled_does_not_warn()
    {
        Bff.OnConfigureApp += app => _ = app.MapReverseProxy(proxyApp =>
        {
            _ = proxyApp.UseAntiforgeryCheck();
        });
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User).WithAntiforgeryCheck());

        await ConfigureBff(BffSetupType.V4Bff);
        _ = await Bff.BrowserClient.Login();

        var response = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);
        response.HttpResponse.IsSuccessStatusCode.ShouldBeTrue();

        WarningLogEntries.ShouldBeEmpty();
    }

    [Fact]
    public async Task reverse_proxy_route_with_antiforgery_check_metadata_but_middleware_not_enabled_warns()
    {
        // Route opts in via WithAntiforgeryCheck(), but the proxy pipeline never calls
        // proxyApp.UseAntiforgeryCheck(), so the check is never actually enforced.
        Bff.OnConfigureApp += app => _ = app.MapReverseProxy();
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User).WithAntiforgeryCheck());

        await ConfigureBff(BffSetupType.V4Bff);
        _ = await Bff.BrowserClient.Login();

        // Request still succeeds - this is a non-breaking, best-effort warning only.
        var response = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);
        response.HttpResponse.IsSuccessStatusCode.ShouldBeTrue();

        var warnings = WarningLogEntries;
        warnings.Count.ShouldBe(1);
        warnings[0].ShouldContain(The.RouteId.ToString());
        warnings[0].ShouldContain("UseAntiforgeryCheck()");
    }

    [Fact]
    public async Task bff_reverse_proxy_endpoint_with_user_token_does_not_warn()
    {
        // MapBffReverseProxy / AsBffApiEndpoint + app.UseBff() (always enabled for BffTestHost)
        // is considered protected.
        Bff.OnConfigureApp += app => _ = app.MapBffReverseProxy();
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User));

        await ConfigureBff(BffSetupType.V4Bff);
        _ = await Bff.BrowserClient.Login();

        var response = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);
        response.HttpResponse.IsSuccessStatusCode.ShouldBeTrue();

        WarningLogEntries.ShouldBeEmpty();
    }

    [Fact]
    public async Task reverse_proxy_route_with_client_token_does_not_warn()
    {
        // Client tokens aren't an ambient user credential, so CSRF protection isn't relevant.
        Bff.OnConfigureApp += app => _ = app.MapReverseProxy();
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.Client));

        await ConfigureBff(BffSetupType.V4Bff);
        _ = await Bff.BrowserClient.Login();

        var response = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);
        response.HttpResponse.IsSuccessStatusCode.ShouldBeTrue();

        WarningLogEntries.ShouldBeEmpty();
    }

    [Fact]
    public async Task cluster_only_antiforgery_check_metadata_with_user_token_still_warns()
    {
        // Regression test: the YARP AntiForgeryMiddleware only ever reads ROUTE metadata
        // (route.Config.Metadata), never cluster metadata. If a cluster sets the
        // AntiforgeryCheck metadata but the route doesn't, the middleware never enforces
        // the header check for this route, so the warning must still fire - even though
        // proxyApp.UseAntiforgeryCheck() is wired up in the pipeline.
        Bff.OnConfigureApp += app => _ = app.MapReverseProxy(proxyApp =>
        {
            _ = proxyApp.UseAntiforgeryCheck();
        });

        var clusterWithAntiforgeryMetadataOnly = Some.ClusterConfig(Api) with
        {
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Constants.Yarp.AntiforgeryCheckMetadata] = "true"
            }
        };
        ConfigureYarp(Some.RouteConfig().WithAccessToken(RequiredTokenType.User), clusterWithAntiforgeryMetadataOnly);

        await ConfigureBff(BffSetupType.V4Bff);
        _ = await Bff.BrowserClient.Login();

        // Still succeeds - the warning is non-breaking.
        var response = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);
        response.HttpResponse.IsSuccessStatusCode.ShouldBeTrue();

        var warnings = WarningLogEntries;
        warnings.Count.ShouldBe(1);
        warnings[0].ShouldContain(The.RouteId.ToString());
        warnings[0].ShouldContain("without anti-forgery protection");
    }

    [Fact]
    public async Task differing_route_and_cluster_antiforgery_check_metadata_does_not_throw_and_route_still_works()
    {
        // Regression test: previously, the anti-forgery metadata was read via the same
        // route-or-cluster merging helper used for the token type metadata, which throws
        // when the route and cluster values disagree. Reading only route metadata (matching
        // what the YARP AntiForgeryMiddleware actually enforces) must never throw, regardless
        // of what the cluster's metadata says.
        Bff.OnConfigureApp += app => _ = app.MapReverseProxy(proxyApp =>
        {
            _ = proxyApp.UseAntiforgeryCheck();
        });

        var clusterWithDifferingMetadata = Some.ClusterConfig(Api) with
        {
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Constants.Yarp.AntiforgeryCheckMetadata] = "false"
            }
        };
        ConfigureYarp(
            Some.RouteConfig().WithAccessToken(RequiredTokenType.User).WithAntiforgeryCheck(),
            clusterWithDifferingMetadata);

        await ConfigureBff(BffSetupType.V4Bff);
        _ = await Bff.BrowserClient.Login();

        // No exception building the route, and the request still succeeds.
        var response = await Bff.BrowserClient.CallBffHostApi(The.PathAndSubPath);
        response.HttpResponse.IsSuccessStatusCode.ShouldBeTrue();

        // The route's own metadata says "true" and the middleware enforced + passed the check,
        // so this is correctly considered protected - the cluster's differing value is ignored.
        WarningLogEntries.ShouldBeEmpty();
    }
}
