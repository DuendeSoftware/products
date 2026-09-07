// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Text.Json;
using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.EntityFramework.Mappers;

/// <summary>
/// Extension methods to map to/from entity/model for SAML logout sessions.
/// </summary>
public static class SamlLogoutSessionMappers
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    extension(SamlLogoutSession model)
    {
        /// <summary>
        /// Maps a <see cref="SamlLogoutSession"/> model to a <see cref="Entities.SamlLogoutSession"/> entity.
        /// </summary>
        /// <param name="expiresAtUtc">The expiration time for the entity.</param>
        /// <returns>The entity.</returns>
        public Entities.SamlLogoutSession ToEntity(DateTime expiresAtUtc) =>
            new()
            {
                LogoutId = model.LogoutId,
                SerializedSession = JsonSerializer.Serialize(model, JsonOptions),
                ExpiresAtUtc = expiresAtUtc,
            };
    }

    extension(Entities.SamlLogoutSession? entity)
    {
        /// <summary>
        /// Maps a <see cref="Entities.SamlLogoutSession"/> entity to a <see cref="SamlLogoutSession"/> model.
        /// </summary>
        /// <returns>The model, or <see langword="null"/> if the entity is null or deserialization fails.</returns>
        public SamlLogoutSession? ToModel()
        {
            if (entity is null)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<SamlLogoutSession>(entity.SerializedSession, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
