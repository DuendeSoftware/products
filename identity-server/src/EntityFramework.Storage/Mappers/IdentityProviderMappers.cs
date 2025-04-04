// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.EntityFramework.Mappers;

/// <summary>
/// Extension methods to map to/from entity/model for identity providers.
/// </summary>
public static class IdentityProviderMappers
{
    /// <summary>
    /// Maps an entity to a model.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns></returns>
    public static Models.IdentityProvider ToModel(this Entities.IdentityProvider entity) => entity == null ? null :
            new Models.IdentityProvider(entity.Type)
            {
                Scheme = entity.Scheme,
                DisplayName = entity.DisplayName,
                Enabled = entity.Enabled,
                Type = entity.Type,
                Properties = PropertiesConverter.Convert(entity.Properties)
            };

    /// <summary>
    /// Maps a model to an entity.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns></returns>
    public static Entities.IdentityProvider ToEntity(this Models.IdentityProvider model) => model == null ? null :
            new Entities.IdentityProvider
            {
                Scheme = model.Scheme,
                DisplayName = model.DisplayName,
                Enabled = model.Enabled,
                Type = model.Type,
                Properties = PropertiesConverter.Convert(model.Properties)
            };
}
