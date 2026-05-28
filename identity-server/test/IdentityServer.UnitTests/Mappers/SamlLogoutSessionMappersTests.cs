// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Saml;
using Entities = Duende.IdentityServer.EntityFramework.Entities;

namespace IdentityServer.UnitTests.Mappers;

public class SamlLogoutSessionMappersTests
{
    private static SamlLogoutSession CreateModel(bool withResponse = false) =>
        new()
        {
            LogoutId = "logout-123",
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["_req-sp1"] = new("https://sp1.example.com",
                    withResponse ? new SamlSpLogoutResponse(true, DateTimeOffset.UtcNow) : null),
                ["_req-sp2"] = new("https://sp2.example.com"),
            },
            CreatedUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        };

    [Fact]
    public void CanMapSamlLogoutSession()
    {
        var model = CreateModel();
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(5);

        var entity = model.ToEntity(expiresAtUtc);
        var roundTripped = entity.ToModel();

        entity.ShouldNotBeNull();
        roundTripped.ShouldNotBeNull();
    }

    [Fact]
    public void RoundTripPreservesAllProperties()
    {
        var model = CreateModel(withResponse: true);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(5);

        var entity = model.ToEntity(expiresAtUtc);
        var roundTripped = entity.ToModel()!;

        roundTripped.LogoutId.ShouldBe(model.LogoutId);
        roundTripped.ExpectedResponses.Count.ShouldBe(2);
        roundTripped.ExpectedResponses["_req-sp1"].SpEntityId.ShouldBe("https://sp1.example.com");
        roundTripped.ExpectedResponses["_req-sp1"].Response.ShouldNotBeNull();
        roundTripped.ExpectedResponses["_req-sp1"].Response!.Success.ShouldBeTrue();
        roundTripped.ExpectedResponses["_req-sp2"].SpEntityId.ShouldBe("https://sp2.example.com");
        roundTripped.ExpectedResponses["_req-sp2"].Response.ShouldBeNull();
        roundTripped.CreatedUtc.ShouldBe(model.CreatedUtc, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void ToEntitySetsLogoutId()
    {
        var model = CreateModel();
        var entity = model.ToEntity(DateTime.UtcNow.AddMinutes(5));

        entity.LogoutId.ShouldBe(model.LogoutId);
    }

    [Fact]
    public void ToEntitySetsExpiresAtUtc()
    {
        var model = CreateModel();
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(5);
        var entity = model.ToEntity(expiresAtUtc);

        entity.ExpiresAtUtc.ShouldBe(expiresAtUtc);
    }

    [Fact]
    public void ToEntitySetsSerializedSession()
    {
        var model = CreateModel();
        var entity = model.ToEntity(DateTime.UtcNow.AddMinutes(5));

        entity.SerializedSession.ShouldNotBeNullOrWhiteSpace();
        entity.SerializedSession.ShouldContain("logout-123");
    }

    [Fact]
    public void ToModelReturnsNullForNullEntity()
    {
        Entities.SamlLogoutSession? entity = null;
        var model = entity.ToModel();
        model.ShouldBeNull();
    }

    [Fact]
    public void ToModelReturnsNullForInvalidJson()
    {
        var entity = new Entities.SamlLogoutSession
        {
            LogoutId = "test",
            SerializedSession = "not valid json",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        };
        var result = entity.ToModel();
        result.ShouldBeNull();
    }

    [Fact]
    public void RoundTripWithNoResponses()
    {
        var model = CreateModel(withResponse: false);
        var entity = model.ToEntity(DateTime.UtcNow.AddMinutes(5));
        var deserialized = entity.ToModel();

        deserialized.ShouldNotBeNull();
        deserialized!.ExpectedResponses["_req-sp1"].Response.ShouldBeNull();
        deserialized.ExpectedResponses["_req-sp2"].Response.ShouldBeNull();
    }

    [Fact]
    public void RoundTripWithResponses()
    {
        var model = CreateModel(withResponse: true);
        var entity = model.ToEntity(DateTime.UtcNow.AddMinutes(5));
        var deserialized = entity.ToModel();

        deserialized.ShouldNotBeNull();
        deserialized!.ExpectedResponses["_req-sp1"].Response.ShouldNotBeNull();
        deserialized.ExpectedResponses["_req-sp1"].Response!.Success.ShouldBeTrue();
    }
}
