// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
        _subject = new EndpointUsageDiagnosticEntry();
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
}
