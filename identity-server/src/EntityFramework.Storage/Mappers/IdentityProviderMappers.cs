// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.EntityFramework.Mappers;

/// <summary>
/// Extension methods to map to/from entity/model for identity providers.
/// </summary>
public static class IdentityProviderMappers
{
    extension(Entities.IdentityProvider entity)
    {
        /// <summary>
        /// Maps an entity to a model.
        /// </summary>
        /// <returns></returns>
        public Models.IdentityProvider ToModel() => entity == null ? null :
                new Models.IdentityProvider(entity.Type)
                {
                    Scheme = entity.Scheme,
                    DisplayName = entity.DisplayName,
                    Enabled = entity.Enabled,
                    Type = entity.Type,
                    Properties = PropertiesConverter.Convert(entity.Properties)
                };
    }

    extension(Models.IdentityProvider model)
    {
        /// <summary>
        /// Maps a model to an entity.
        /// </summary>
        /// <returns></returns>
        public Entities.IdentityProvider ToEntity() => model == null ? null :
                new Entities.IdentityProvider
                {
                    Scheme = model.Scheme,
                    DisplayName = model.DisplayName,
                    Enabled = model.Enabled,
                    Type = model.Type,
                    Properties = PropertiesConverter.Convert(model.Properties)
                };
    }
}
