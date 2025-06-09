// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Licensing.V2;

public class ResourceLoadedTrackerTests
{
    private readonly ResourceLoadedTracker _subject = new();

    [Fact]
    public void Tracks_All_Resource_Types()
    {
        var resources = new Resources(
            [new IdentityResource("identity", ["claim1", "claim2"])],
            [new ApiResource("api1") { RequireResourceIndicator = true, ApiSecrets = [new Secret { Type = "Test" }] }],
            [new ApiScope("scope1")]
        );

        _subject.TrackResources(resources);

        _subject.Resources.Keys.ShouldContain($"ApiResource:api1");
        _subject.Resources.Keys.ShouldContain("IdentityResource:identity");
        _subject.Resources.Keys.ShouldContain("ApiScope:scope1");

        var apiResource = _subject.Resources["ApiResource:api1"];
        apiResource.Type.ShouldBe("ApiResource");
        apiResource.Name.ShouldBe("api1");
        apiResource.ResourceIndicatorRequired.GetValueOrDefault().ShouldBeTrue();
        apiResource.SecretTypes.ShouldBe(["Test"]);
    }

    [Fact]
    public void Does_Not_Track_Duplicate_Resources()
    {
        var resources = new Resources(
            [new IdentityResource("identity", ["openid"])],
            [new ApiResource("api1")],
            [new ApiScope("scope1")]
        );

        _subject.TrackResources(resources);
        _subject.TrackResources(resources);

        _subject.Resources.Count.ShouldBe(3);
    }

    [Fact]
    public void Enforces_MaxResourceTrackedCount()
    {
        var apiResources = Enumerable.Range(0, 150)
            .Select(i => new ApiResource($"api{i}")).ToArray();
        var resources = new Resources([], apiResources, []);

        _subject.TrackResources(resources);

        _subject.Resources.Count.ShouldBe(100);
    }

    [Fact]
    public void Tracks_SecretTypes_Distinctly()
    {
        var apiResource = new ApiResource("api1")
        {
            ApiSecrets =
            [
                new Secret("secret1") { Type = "type1"},
                    new Secret("secret2") { Type = "type1"},
                    new Secret("secret3") { Type = "type2" }
            ]
        };
        var resources = new Resources([], [apiResource], []);

        _subject.TrackResources(resources);

        var trackedResource = _subject.Resources["ApiResource:api1"];
        trackedResource.SecretTypes.ShouldBe(["type1", "type2"], ignoreOrder: true);
    }
}
