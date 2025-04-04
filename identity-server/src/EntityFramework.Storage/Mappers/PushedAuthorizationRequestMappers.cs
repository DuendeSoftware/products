// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Entities;

namespace Duende.IdentityServer.EntityFramework.Mappers;

/// <summary>
/// Extension methods to map to/from entity/model for pushed authorization requests.
/// </summary>
public static class PushedAuthorizationRequestMappers
{
    /// <summary>
    /// Maps an entity to a model.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns></returns>
    public static Models.PushedAuthorizationRequest ToModel(this PushedAuthorizationRequest entity) => entity == null ? null :
            new Models.PushedAuthorizationRequest
            {
                ReferenceValueHash = entity.ReferenceValueHash,
                ExpiresAtUtc = entity.ExpiresAtUtc,
                Parameters = entity.Parameters,
            };

    /// <summary>
    /// Maps a model to an entity.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns></returns>
    public static Entities.PushedAuthorizationRequest ToEntity(this Models.PushedAuthorizationRequest model) => model == null ? null :
            new Entities.PushedAuthorizationRequest
            {
                ReferenceValueHash = model.ReferenceValueHash,
                ExpiresAtUtc = model.ExpiresAtUtc,
                Parameters = model.Parameters,
            };
}
