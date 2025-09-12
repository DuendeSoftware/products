// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.EntityFramework.Mappers;

/// <summary>
/// Extension methods to map to/from entity/model for identity resources.
/// </summary>
public static class IdentityResourceMappers
{
    /// <summary>
    /// Maps an entity to a model.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns></returns>
    public static Models.IdentityResource ToModel(this Entities.IdentityResource entity) => entity == null ? null :
            new Models.IdentityResource
            {
                Enabled = entity.Enabled,
                Name = entity.Name,
                DisplayName = entity.DisplayName,
                Description = entity.Description,
                ShowInDiscoveryDocument = entity.ShowInDiscoveryDocument,
                UserClaims = entity.UserClaims?.Select(c => c.Type).ToList() ?? [],
                Properties = entity.Properties?.ToDictionary(p => p.Key, p => p.Value) ?? [],

                Required = entity.Required,
                Emphasize = entity.Emphasize
            };

    /// <summary>
    /// Maps a model to an entity.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns></returns>
    public static Entities.IdentityResource ToEntity(this Models.IdentityResource model) => model == null ? null :
            new Entities.IdentityResource
            {
                Enabled = model.Enabled,
                Name = model.Name,
                DisplayName = model.DisplayName,
                Description = model.Description,
                ShowInDiscoveryDocument = model.ShowInDiscoveryDocument,
                UserClaims = model.UserClaims?.Select(c => new Entities.IdentityResourceClaim
                {
                    Type = c,
                }).ToList() ?? [],
                Properties = model.Properties?.Select(p => new Entities.IdentityResourceProperty
                {
                    Key = p.Key,
                    Value = p.Value
                }).ToList() ?? [],

                Required = model.Required,
                Emphasize = model.Emphasize
            };
}
