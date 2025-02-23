// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Linq;
using Duende.IdentityServer.EntityFramework.Mappers;
using Models = Duende.IdentityServer.Models;
using Entities = Duende.IdentityServer.EntityFramework.Entities;
using Shouldly;
using Xunit;

namespace EntityFramework.Storage.UnitTests.Mappers;

public class ScopesMappersTests
{
    [Fact]
    public void CanMapScope()
    {
        var model = new Models.ApiScope();
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
            "Created",
            "LastAccessed",
            "NonEditable"
        };

        MapperTestHelpers
            .AllPropertiesAreMapped<Models.ApiScope, Entities.ApiScope>(source => source.ToEntity(), excludedProperties, out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }

    [Fact]
    public void mapping_entity_to_model_maps_all_properties()
    {
        MapperTestHelpers
            .AllPropertiesAreMapped<Entities.ApiScope, Models.ApiScope>(source => source.ToModel(), out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }

    [Fact]
    public void Properties_Map()
    {
        var model = new Duende.IdentityServer.Models.ApiScope()
        {
            Description = "description",
            DisplayName = "displayname",
            Name = "foo",
            UserClaims = { "c1", "c2" },
            Properties = {
                { "x", "xx" },
                { "y", "yy" },
            },
            Enabled = false
        };


        var mappedEntity = model.ToEntity();
        mappedEntity.Description.ShouldBe("description");
        mappedEntity.DisplayName.ShouldBe("displayname");
        mappedEntity.Name.ShouldBe("foo");

        mappedEntity.UserClaims.Count.ShouldBe(2);
        mappedEntity.UserClaims.Select(x => x.Type).ShouldBe(["c1", "c2"]);
        mappedEntity.Properties.Count.ShouldBe(2);
        mappedEntity.Properties.ShouldContain(x => x.Key == "x" && x.Value == "xx");
        mappedEntity.Properties.ShouldContain(x => x.Key == "y" && x.Value == "yy");


        var mappedModel = mappedEntity.ToModel();

        mappedModel.Description.ShouldBe("description");
        mappedModel.DisplayName.ShouldBe("displayname");
        mappedModel.Enabled.ShouldBeFalse();
        mappedModel.Name.ShouldBe("foo");
        mappedModel.UserClaims.Count.ShouldBe(2);
        mappedModel.UserClaims.ShouldBe(["c1", "c2"], true);
        mappedModel.Properties.Count.ShouldBe(2);
        mappedModel.Properties["x"].ShouldBe("xx");
        mappedModel.Properties["y"].ShouldBe("yy");
    }
}