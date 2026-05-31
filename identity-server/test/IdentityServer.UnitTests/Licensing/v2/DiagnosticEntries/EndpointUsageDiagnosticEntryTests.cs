// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Endpoint = Duende.IdentityServer.Hosting.Endpoint;

namespace IdentityServer.UnitTests.Licensing.v2.DiagnosticEntries;

public class EndpointUsageDiagnosticEntryTests
{
    private readonly List<PathString> _endpoints;
    private readonly EndpointUsageDiagnosticEntry _subject;

    public EndpointUsageDiagnosticEntryTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddIdentityServer().AddDefaultEndpoints();
        _endpoints = serviceCollection.Select(descriptor => descriptor.ImplementationInstance as Endpoint)
            .Where(endpoint => endpoint != null)
            .Select(endpoint => endpoint.Path)
            .Distinct()
            .ToList();
        _subject = new EndpointUsageDiagnosticEntry(Options.Create(new IdentityServerOptions()));
    }

    [Fact]
    public async Task Should_Handle_Counts_For_All_Endpoints()
    {
        foreach (var endpoint in _endpoints)
        {
            Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", endpoint);
        }

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var endpointUsage = result.RootElement.GetProperty("EndpointUsage");
        foreach (var endpoint in _endpoints)
        {
            endpointUsage.GetProperty(endpoint.Value!).GetInt64().ShouldBe(1);
        }
    }

    [Fact]
    public async Task Should_Handle_Multiple_Requests_For_Same_Endpoint()
    {
        var route = IdentityServerConstants.ProtocolRoutePaths.Authorize.EnsureLeadingSlash();
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", route);
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", route);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var endpointUsage = result.RootElement.GetProperty("EndpointUsage");
        endpointUsage.GetProperty(route).GetInt64().ShouldBe(2);
    }

    [Fact]
    public async Task Should_Handle_Unknown_Endpoints()
    {
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", "/unknown/endpoint");

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var endpointUsage = result.RootElement.GetProperty("EndpointUsage");
        endpointUsage.TryGetProperty("/unknown/endpoint", out _).ShouldBeFalse();
        endpointUsage.GetProperty("other").GetInt64().ShouldBe(1);
        foreach (var endpoint in _endpoints)
        {
            endpointUsage.GetProperty(endpoint.Value!).GetInt64().ShouldBe(0);
        }
    }

    [Fact]
    public async Task Should_Ignore_Other_Telemetry_Counters()
    {
        var route = IdentityServerConstants.ProtocolRoutePaths.Authorize.EnsureLeadingSlash();
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", route);
        Duende.IdentityServer.Telemetry.Metrics.DecreaseActiveRequests("", route);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var endpointUsage = result.RootElement.GetProperty("EndpointUsage");
        endpointUsage.GetProperty(route).GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Match_Custom_Saml_Paths()
    {
        var options = new IdentityServerOptions();
        options.Saml.EntityIdPath = "/custom/metadata";
        options.Saml.Endpoints.SingleSignOnServicePath = "/custom/sso";
        options.Saml.Endpoints.SingleSignOnCallbackPath = "/custom/sso/callback";
        options.Saml.Endpoints.SingleLogoutServicePath = "/custom/slo";
        options.Saml.Endpoints.SingleLogoutCallbackPath = "/custom/slo/callback";

        using var subject = new EndpointUsageDiagnosticEntry(Options.Create(options));

        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", "/custom/metadata");
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", "/custom/sso");
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", "/custom/sso/callback");
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", "/custom/slo");
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", "/custom/slo/callback");

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var endpointUsage = result.RootElement.GetProperty("EndpointUsage");
        endpointUsage.GetProperty("/custom/metadata").GetInt64().ShouldBe(1);
        endpointUsage.GetProperty("/custom/sso").GetInt64().ShouldBe(1);
        endpointUsage.GetProperty("/custom/sso/callback").GetInt64().ShouldBe(1);
        endpointUsage.GetProperty("/custom/slo").GetInt64().ShouldBe(1);
        endpointUsage.GetProperty("/custom/slo/callback").GetInt64().ShouldBe(1);
        endpointUsage.GetProperty("other").GetInt64().ShouldBe(0);
    }

    [Fact]
    public async Task Should_Not_Match_Default_Paths_When_Custom_Paths_Configured()
    {
        var options = new IdentityServerOptions();
        options.Saml.EntityIdPath = "/custom/metadata";
        options.Saml.Endpoints.SingleSignOnServicePath = "/custom/sso";
        options.Saml.Endpoints.SingleSignOnCallbackPath = "/custom/sso/callback";
        options.Saml.Endpoints.SingleLogoutServicePath = "/custom/slo";
        options.Saml.Endpoints.SingleLogoutCallbackPath = "/custom/slo/callback";

        using var subject = new EndpointUsageDiagnosticEntry(Options.Create(options));

        // Send requests to the default paths — they should go to "other"
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", "/Saml2");
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", "/Saml2/SSO");
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", "/Saml2/SLO");

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var endpointUsage = result.RootElement.GetProperty("EndpointUsage");
        endpointUsage.GetProperty("/custom/metadata").GetInt64().ShouldBe(0);
        endpointUsage.GetProperty("/custom/sso").GetInt64().ShouldBe(0);
        endpointUsage.GetProperty("/custom/slo").GetInt64().ShouldBe(0);
        endpointUsage.GetProperty("other").GetInt64().ShouldBe(3);
    }

    [Fact]
    public async Task Should_Resolve_Metadata_Path_From_Absolute_EntityId()
    {
        var options = new IdentityServerOptions();
        options.Saml.EntityId = "https://idp.example.com/saml/metadata";

        using var subject = new EndpointUsageDiagnosticEntry(Options.Create(options));

        // The metadata path should be resolved from the EntityId URL's path component
        Duende.IdentityServer.Telemetry.Metrics.IncreaseActiveRequests("", "/saml/metadata");

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var endpointUsage = result.RootElement.GetProperty("EndpointUsage");
        endpointUsage.GetProperty("/saml/metadata").GetInt64().ShouldBe(1);
        endpointUsage.GetProperty("other").GetInt64().ShouldBe(0);
    }
}
