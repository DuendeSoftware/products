// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.EntityFramework.Mappers;

/// <summary>
/// Extension methods to map to/from entity/model for SAML signin state.
/// </summary>
public static class SamlSigninStateMappers
{
    extension(SamlAuthenticationState model)
    {
        /// <summary>
        /// Maps a <see cref="SamlAuthenticationState"/> model to a <see cref="SamlSigninState"/> entity.
        /// </summary>
        /// <param name="stateId">The state identifier to assign.</param>
        /// <param name="expiresAtUtc">The expiration time for the entity.</param>
        /// <param name="serializer">The serializer to use for the state.</param>
        /// <returns>The entity.</returns>
        public SamlSigninState ToEntity(Guid stateId, DateTime expiresAtUtc, ISamlSigninStateSerializer serializer) =>
            new()
            {
                StateId = stateId,
                SerializedState = serializer.Serialize(model),
                ExpiresAtUtc = expiresAtUtc,
                ServiceProviderEntityId = model.ServiceProviderEntityId,
            };
    }

    extension(SamlSigninState? entity)
    {
        /// <summary>
        /// Maps a <see cref="SamlSigninState"/> entity to a <see cref="SamlAuthenticationState"/> model.
        /// </summary>
        /// <param name="serializer">The serializer to use for the state.</param>
        /// <returns>The model, or <see langword="null"/> if the entity is null.</returns>
        public SamlAuthenticationState? ToModel(ISamlSigninStateSerializer serializer)
        {
            if (entity is null)
            {
                return null;
            }

            return serializer.Deserialize(entity.SerializedState);
        }
    }
}
