// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Mappers;
using Entities = Duende.IdentityServer.EntityFramework.Entities;
using Models = Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Mappers;

public class ApiResourceMappersTests
{
    [Fact]
    public void Can_Map()
    {
        var model = new Models.ApiResource();
        var mappedEntity = model.ToEntity();
        var mappedModel = mappedEntity.ToModel();

        mappedModel.ShouldNotBeNull();
        mappedEntity.ShouldNotBeNull();
    }

    [Fact]
    public void Properties_Map()
    {
        var model = new Models.ApiResource()
        {
            Description = "description",
            DisplayName = "displayname",
            Name = "foo",
            Scopes = { "foo1", "foo2" },
            Enabled = false
        };


        var mappedEntity = model.ToEntity();

        mappedEntity.Scopes.Count.ShouldBe(2);
        var foo1 = mappedEntity.Scopes.FirstOrDefault(x => x.Scope == "foo1");
        foo1.ShouldNotBeNull();
        var foo2 = mappedEntity.Scopes.FirstOrDefault(x => x.Scope == "foo2");
        foo2.ShouldNotBeNull();


        var mappedModel = mappedEntity.ToModel();

        mappedModel.Description.ShouldBe("description");
        mappedModel.DisplayName.ShouldBe("displayname");
        mappedModel.Enabled.ShouldBeFalse();
        mappedModel.Name.ShouldBe("foo");
    }

    [Fact]
    public void missing_values_should_use_defaults()
    {
        var entity = new Entities.ApiResource
        {
            Secrets = new System.Collections.Generic.List<Entities.ApiResourceSecret>
            {
                new Entities.ApiResourceSecret
                {
                }
            }
        };

        var def = new Models.ApiResource
        {
            ApiSecrets = { new Models.Secret("foo") }
        };

        var model = entity.ToModel();
        model.ApiSecrets.First().Type.ShouldBe(def.ApiSecrets.First().Type);
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
            .AllPropertiesAreMapped<Models.ApiResource, Entities.ApiResource>(
                source => source.AllowedAccessTokenSigningAlgorithms.Add("RS256"),
                source => source.ToEntity(),
                excludedProperties,
                out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }

    [Fact]
    public void mapping_entity_to_model_maps_all_properties() => MapperTestHelpers
            .AllPropertiesAreMapped<Entities.ApiResource, Models.ApiResource>(source => source.ToModel(), out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
}
