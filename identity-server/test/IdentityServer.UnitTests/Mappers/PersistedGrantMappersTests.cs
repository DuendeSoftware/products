// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Mappers;
using Entities = Duende.IdentityServer.EntityFramework.Entities;
using Models = Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Mappers;

public class PersistedGrantMappersTests
{
    [Fact]
    public void CanMap()
    {
        var model = new Duende.IdentityServer.Models.PersistedGrant()
        {
            ConsumedTime = new System.DateTime(2020, 02, 03, 4, 5, 6)
        };

        var mappedEntity = model.ToEntity();
        mappedEntity.ConsumedTime.Value.ShouldBe(new System.DateTime(2020, 02, 03, 4, 5, 6));

        var mappedModel = mappedEntity.ToModel();
        mappedModel.ConsumedTime.Value.ShouldBe(new System.DateTime(2020, 02, 03, 4, 5, 6));

        mappedModel.ShouldNotBeNull();
        mappedEntity.ShouldNotBeNull();
    }

    [Fact]
    public void mapping_model_to_entity_maps_all_properties()
    {
        var excludedProperties = new string[]
        {
            "Id",
            "Updated",
            "Created",
            "LastAccessed",
            "NonEditable"
        };

        MapperTestHelpers
            .AllPropertiesAreMapped<Models.PersistedGrant, Entities.PersistedGrant>(source => source.ToEntity(), excludedProperties, out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }

    [Fact]
    public void mapping_entity_to_model_maps_all_properties() => MapperTestHelpers
            .AllPropertiesAreMapped<Entities.PersistedGrant, Models.PersistedGrant>(source => source.ToModel(), out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
}
