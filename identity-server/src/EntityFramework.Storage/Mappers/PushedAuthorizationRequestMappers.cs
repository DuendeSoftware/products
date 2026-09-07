// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Entities;

namespace Duende.IdentityServer.EntityFramework.Mappers;

/// <summary>
/// Extension methods to map to/from entity/model for pushed authorization requests.
/// </summary>
public static class PushedAuthorizationRequestMappers
{
    extension(PushedAuthorizationRequest entity)
    {
        /// <summary>
        /// Maps an entity to a model.
        /// </summary>
        /// <returns></returns>
        public Models.PushedAuthorizationRequest ToModel() => entity == null ? null :
                new Models.PushedAuthorizationRequest
                {
                    ReferenceValueHash = entity.ReferenceValueHash,
                    ExpiresAtUtc = entity.ExpiresAtUtc,
                    Parameters = entity.Parameters,
                };
    }

    extension(Models.PushedAuthorizationRequest model)
    {
        /// <summary>
        /// Maps a model to an entity.
        /// </summary>
        /// <returns></returns>
        public Entities.PushedAuthorizationRequest ToEntity() => model == null ? null :
                new Entities.PushedAuthorizationRequest
                {
                    ReferenceValueHash = model.ReferenceValueHash,
                    ExpiresAtUtc = model.ExpiresAtUtc,
                    Parameters = model.Parameters,
                };
    }
}
