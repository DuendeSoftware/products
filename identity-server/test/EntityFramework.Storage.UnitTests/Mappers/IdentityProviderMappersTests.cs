// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Mappers;
using FluentAssertions;
using Xunit;
using Entities = Duende.IdentityServer.EntityFramework.Entities;
using Models = Duende.IdentityServer.Models;

namespace EntityFramework.Storage.UnitTests.Mappers;

public class IdentityProviderMappersTests
{
    [Fact]
    public void CanMapIdp()
    {
        var model = new Models.OidcProvider();
        var mappedEntity = model.ToEntity();
        var mappedModel = mappedEntity.ToModel();

        Assert.NotNull(mappedModel);
        Assert.NotNull(mappedEntity);
    }

    [Fact]
    public void Properties_Map()
    {
        var model = new Models.OidcProvider()
        {
            Enabled = false,
            Authority = "auth",
            ClientId = "client",
            ClientSecret = "secret",
            DisplayName = "name",
            ResponseType = "rt",
            Scheme = "scheme",
            Scope = "scope",
        };


        var mappedEntity = model.ToEntity();
        mappedEntity.DisplayName.Should().Be("name");
        mappedEntity.Scheme.Should().Be("scheme");
        mappedEntity.Type.Should().Be("oidc");
        mappedEntity.Properties.Should().NotBeNullOrEmpty();


        var mappedModel = new Models.OidcProvider(mappedEntity.ToModel());

        mappedModel.Authority.Should().Be("auth");
        mappedModel.ClientId.Should().Be("client");
        mappedModel.ClientSecret.Should().Be("secret");
        mappedModel.DisplayName.Should().Be("name");
        mappedModel.ResponseType.Should().Be("rt");
        mappedModel.Scheme.Should().Be("scheme");
        mappedModel.Scope.Should().Be("scope");
        mappedModel.Type.Should().Be("oidc");
    }

    [Fact]
    public void mapping_model_to_entity_maps_all_properties()
    {
        var excludedProperties = new string[]
        {
            "Id",
            "Updated",
            "LastAccessed",
            "NonEditable"
        };

        MapperTestHelpers
            .AllPropertiesAreMapped<Models.IdentityProvider, Entities.IdentityProvider>(
                () => new Models.IdentityProvider("type"),
                source => source.ToEntity(),
                excludedProperties,
                out var unmappedMembers)
            .Should()
            .BeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }

    [Fact]
    public void mapping_entity_to_model_maps_all_properties()
    {
        MapperTestHelpers
            .AllPropertiesAreMapped<Entities.IdentityProvider, Models.IdentityProvider>(
                source =>
                {
                    source.Properties =
                    """
                    {
                        "foo": "bar"
                    }
                    """;
                },
                source => source.ToModel(),
                out var unmappedMembers)
            .Should()
            .BeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }
}
