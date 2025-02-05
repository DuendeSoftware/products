// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Mappers;
using Shouldly;
using Xunit;
using Models = Duende.IdentityServer.Models;
using Entities = Duende.IdentityServer.EntityFramework.Entities;

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
        mappedEntity.DisplayName.ShouldBe("name");
        mappedEntity.Scheme.ShouldBe("scheme");
        mappedEntity.Type.ShouldBe("oidc");
        mappedEntity.Properties.ShouldNotBeNullOrEmpty();


        var mappedModel = new Models.OidcProvider(mappedEntity.ToModel());

        mappedModel.Authority.ShouldBe("auth");
        mappedModel.ClientId.ShouldBe("client");
        mappedModel.ClientSecret.ShouldBe("secret");
        mappedModel.DisplayName.ShouldBe("name");
        mappedModel.ResponseType.ShouldBe("rt");
        mappedModel.Scheme.ShouldBe("scheme");
        mappedModel.Scope.ShouldBe("scope");
        mappedModel.Type.ShouldBe("oidc");
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
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
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
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }
}
