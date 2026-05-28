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
    /// <summary>
    /// Maps a <see cref="SamlAuthenticationState"/> model to a <see cref="SamlSigninState"/> entity.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="stateId">The state identifier to assign.</param>
    /// <param name="expiresAtUtc">The expiration time for the entity.</param>
    /// <param name="serializer">The serializer to use for the state.</param>
    /// <returns>The entity.</returns>
    public static SamlSigninState ToEntity(this SamlAuthenticationState model, Guid stateId, DateTime expiresAtUtc, ISamlSigninStateSerializer serializer) =>
        new()
        {
            StateId = stateId,
            SerializedState = serializer.Serialize(model),
            ExpiresAtUtc = expiresAtUtc,
            ServiceProviderEntityId = model.ServiceProviderEntityId,
        };

    /// <summary>
    /// Maps a <see cref="SamlSigninState"/> entity to a <see cref="SamlAuthenticationState"/> model.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="serializer">The serializer to use for the state.</param>
    /// <returns>The model, or <see langword="null"/> if the entity is null.</returns>
    public static SamlAuthenticationState? ToModel(this SamlSigninState? entity, ISamlSigninStateSerializer serializer)
    {
        if (entity is null)
        {
            return null;
        }

        return serializer.Deserialize(entity.SerializedState);
    }
}
