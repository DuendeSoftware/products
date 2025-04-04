// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Mappers;
using Entities = Duende.IdentityServer.EntityFramework.Entities;
using Models = Duende.IdentityServer.Models;

namespace EntityFramework.Storage.UnitTests.Mappers;

public class PushedAuthorizationRequestMappersTests
{
    [Fact]
    public void CanMapPushedAuthorizationRequest()
    {
        var model = new Models.PushedAuthorizationRequest();
        var mappedEntity = model.ToEntity();
        var mappedModel = mappedEntity.ToModel();

        mappedModel.ShouldNotBeNull();
        mappedEntity.ShouldNotBeNull();
    }

    [Fact]
    public void mapping_model_to_entity_maps_all_properties()
    {
        var excludedProperties = new string[]
        {
            "Id",
        };

        MapperTestHelpers
            .AllPropertiesAreMapped<Models.PushedAuthorizationRequest, Entities.PushedAuthorizationRequest>(source => source.ToEntity(), excludedProperties, out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }

    [Fact]
    public void mapping_entity_to_model_maps_all_properties() => MapperTestHelpers
            .AllPropertiesAreMapped<Entities.PushedAuthorizationRequest, Models.PushedAuthorizationRequest>(source => source.ToModel(), out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
}
