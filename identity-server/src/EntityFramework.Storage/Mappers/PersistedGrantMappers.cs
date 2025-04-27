// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.EntityFramework.Mappers;

/// <summary>
/// Extension methods to map to/from entity/model for persisted grants.
/// </summary>
public static class PersistedGrantMappers
{
    /// <summary>
    /// Maps an entity to a model.
    /// </summary>
    /// <param name="entity">The entity.</param>
    public static PersistedGrant ToModel(this Entities.PersistedGrant entity) => entity == null ? null :
            new PersistedGrant
            {
                Key = entity.Key,
                Type = entity.Type,
                SubjectId = entity.SubjectId,
                SessionId = entity.SessionId,
                ClientId = entity.ClientId,
                Description = entity.Description,
                CreationTime = entity.CreationTime,
                Expiration = entity.Expiration,
                ConsumedTime = entity.ConsumedTime,
                Data = entity.Data
            };

    /// <summary>
    /// Maps a model to an entity.
    /// </summary>
    /// <param name="model">The model.</param>
    public static Entities.PersistedGrant ToEntity(this Models.PersistedGrant model) => model == null ? null :
            new Entities.PersistedGrant
            {
                Key = model.Key,
                Type = model.Type,
                SubjectId = model.SubjectId,
                SessionId = model.SessionId,
                ClientId = model.ClientId,
                Description = model.Description,
                CreationTime = model.CreationTime,
                Expiration = model.Expiration,
                ConsumedTime = model.ConsumedTime,
                Data = model.Data
            };

    /// <summary>
    /// Updates an entity from a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="entity">The entity.</param>
    public static void UpdateEntity(this PersistedGrant model, Duende.IdentityServer.EntityFramework.Entities.PersistedGrant entity)
    {
        entity.Key = model.Key;
        entity.Type = model.Type;
        entity.SubjectId = model.SubjectId;
        entity.SessionId = model.SessionId;
        entity.ClientId = model.ClientId;
        entity.Description = model.Description;
        entity.CreationTime = model.CreationTime;
        entity.Expiration = model.Expiration;
        entity.ConsumedTime = model.ConsumedTime;
        entity.Data = model.Data;
    }
}
