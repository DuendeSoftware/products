// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Models;
using IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

namespace IdentityServer.UnitTests.Licensing.v2.DiagnosticEntries;

public class ResourceInfoDiagnosticEntryTests
{
    [Fact]
    public async Task Should_Write_Resource_Info()
    {
        var tracker = new ResourceLoadedTracker();
        tracker.TrackResources(new Resources
        {
            ApiResources = new List<ApiResource>
            {
                new("TestApi", "Test API")
                {
                    RequireResourceIndicator = true,
                    ApiSecrets = new List<Secret> { new Secret { Type = "Test" } }
                }
            },
            IdentityResources = new List<IdentityResource>
            {
                new("TestIdentity", ["IrrelevantClaim"])
            },
            ApiScopes = new List<ApiScope>
            {
                new("TestScope", "Test Scope")
            }
        });
        var subject = new ResourceInfoDiagnosticEntry(tracker);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var resources = result.RootElement.GetProperty("Resources");
        var apiResources = resources.GetProperty("ApiResource").EnumerateArray();
        var apiResource = apiResources.First();
        apiResource.GetProperty("Name").GetString().ShouldBe("TestApi");
        apiResource.GetProperty("ResourceIndicatorRequired").GetBoolean().ShouldBeTrue();
        apiResource.TryGetStringArray("SecretTypes").ShouldBe(["Test"]);
        resources.TryGetStringArray("IdentityResource").ShouldBe(["TestIdentity"]);
        resources.TryGetStringArray("ApiScope").ShouldBe(["TestScope"]);
    }

    [Fact]
    public async Task Should_Write_Empty_Resource_Info()
    {
        var tracker = new ResourceLoadedTracker();
        var subject = new ResourceInfoDiagnosticEntry(tracker);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        result.RootElement.GetProperty("Resources").GetRawText().ShouldBe("{}");
    }

    [Fact]
    public async Task Should_Write_Resource_Info_With_Empty_Secret_Types()
    {
        var tracker = new ResourceLoadedTracker();
        tracker.TrackResources(new Resources
        {
            ApiResources = new List<ApiResource>
            {
                new("TestApi", "Test API")
                {
                    RequireResourceIndicator = true,
                    ApiSecrets = new List<Secret>()
                }
            }
        });
        var subject = new ResourceInfoDiagnosticEntry(tracker);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var resources = result.RootElement.GetProperty("Resources");
        var apiResources = resources.GetProperty("ApiResource").EnumerateArray();
        var apiResource = apiResources.First();
        apiResource.GetProperty("Name").GetString().ShouldBe("TestApi");
        apiResource.GetProperty("ResourceIndicatorRequired").GetBoolean().ShouldBeTrue();
        apiResource.TryGetStringArray("SecretTypes").ShouldBe([]);
    }

    [Fact]
    public async Task Should_Write_Multiple_Resources()
    {
        var tracker = new ResourceLoadedTracker();
        tracker.TrackResources(new Resources
        {
            ApiResources = new List<ApiResource>
            {
                new("ApiResourceOne", "Test API 1")
                {
                    RequireResourceIndicator = true
                },
                new("ApiResourceTwo", "Test API 2")
                {
                    RequireResourceIndicator = false,
                    ApiSecrets = new List<Secret> { new() { Type = "SecretType" } }
                }
            },
            IdentityResources = new List<IdentityResource>
            {
                new("IdentityResourceOne", ["Claim1"]),
                new("IdentityResourceTwo", ["Claim2"])
            },
            ApiScopes = new List<ApiScope>
            {
                new("ApiScopeOne", "Test Scope 1"),
                new("ApiScopeTwo", "Test Scope 2")
            }
        });
        var subject = new ResourceInfoDiagnosticEntry(tracker);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var resources = result.RootElement.GetProperty("Resources");
        var apiResources = resources.GetProperty("ApiResource").EnumerateArray().OrderBy(resource => resource.GetProperty("Name").GetString()).ToList();
        var firstApiResource = apiResources.First();
        firstApiResource.GetProperty("Name").GetString().ShouldBe("ApiResourceOne");
        firstApiResource.GetProperty("ResourceIndicatorRequired").GetBoolean().ShouldBeTrue();
        firstApiResource.TryGetStringArray("SecretTypes").ShouldBe([]);
        var secondApiResource = apiResources.Last();
        secondApiResource.GetProperty("Name").GetString().ShouldBe("ApiResourceTwo");
        secondApiResource.GetProperty("ResourceIndicatorRequired").GetBoolean().ShouldBeFalse();
        secondApiResource.TryGetStringArray("SecretTypes").ShouldBe(["SecretType"]);
        resources.TryGetStringArray("IdentityResource").ShouldBe(["IdentityResourceOne", "IdentityResourceTwo"], ignoreOrder: true);
        resources.TryGetStringArray("ApiScope").ShouldBe(["ApiScopeOne", "ApiScopeTwo"], ignoreOrder: true);
    }
}
