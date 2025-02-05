// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Mappers;
using Xunit;
using Models = Duende.IdentityServer.Models;
using Entities = Duende.IdentityServer.EntityFramework.Entities;
using Shouldly;

namespace EntityFramework.Storage.UnitTests.Mappers;

public class IdentityResourcesMappersTests
{
    [Fact]
    public void CanMapIdentityResources()
    {
        var model = new Models.IdentityResource();
        var mappedEntity = model.ToEntity();
        var mappedModel = mappedEntity.ToModel();

        Assert.NotNull(mappedModel);
        Assert.NotNull(mappedEntity);
    }

    [Fact]
    public void mapping_model_to_entity_maps_all_properties()
    {
        var excludedProperties = new string[]
        {
            "Id",
            "Updated",
            "NonEditable"
        };

        MapperTestHelpers
            .AllPropertiesAreMapped<Models.IdentityResource, Entities.IdentityResource>(
                source => source.ToEntity(),
                excludedProperties,
                out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }

    [Fact]
    public void mapping_entity_to_model_maps_all_properties()
    {
        MapperTestHelpers
            .AllPropertiesAreMapped<Entities.IdentityResource, Models.IdentityResource>(
                source => source.ToModel(),
                out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }
}
