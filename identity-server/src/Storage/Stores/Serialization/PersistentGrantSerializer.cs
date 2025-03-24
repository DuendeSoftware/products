// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.DataProtection;

namespace Duende.IdentityServer.Stores.Serialization;

/// <summary>
/// JSON-based persisted grant serializer
/// </summary>
/// <seealso cref="IPersistentGrantSerializer" />
public class PersistentGrantSerializer : IPersistentGrantSerializer
{
    private static readonly JsonSerializerOptions Settings;

    private readonly PersistentGrantOptions _options;
    private readonly IDataProtector _provider;

    static PersistentGrantSerializer()
    {
        Settings = new JsonSerializerOptions
        {
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        Settings.Converters.Add(new ClaimConverter());
        Settings.Converters.Add(new ClaimsPrincipalConverter());
    }

    /// <summary>
    /// Ctor.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="dataProtectionProvider"></param>
    public PersistentGrantSerializer(PersistentGrantOptions options = null, IDataProtectionProvider dataProtectionProvider = null)
    {
        _options = options;
        _provider = dataProtectionProvider?.CreateProtector(nameof(PersistentGrantSerializer));
    }

    private bool ShouldDataProtect => _options?.DataProtectData == true && _provider != null;

    /// <summary>
    /// Serializes the specified value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public string Serialize<T>(T value)
    {
        var payload = JsonSerializer.Serialize(value, Settings);

        if (ShouldDataProtect)
        {
            payload = _provider.Protect(payload);
        }

        var data = new PersistentGrantDataContainer
        {
            PersistentGrantDataContainerVersion = 1,
            DataProtected = ShouldDataProtect,
            Payload = payload,
        };

        return JsonSerializer.Serialize(data, Settings);
    }

    /// <summary>
    /// Deserializes the specified string.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="json">The json.</param>
    /// <returns></returns>
    public T Deserialize<T>(string json)
    {
        var container = JsonSerializer.Deserialize<PersistentGrantDataContainer>(json, Settings);

        if (container.PersistentGrantDataContainerVersion == 0)
        {
            var item = JsonSerializer.Deserialize<T>(json, Settings);
            PostProcess(item as RefreshToken);
            return item;
        }

        if (container.PersistentGrantDataContainerVersion == 1)
        {
            var payload = container.Payload;

            if (container.DataProtected)
            {
                if (_provider == null)
                {
                    throw new Exception("No IDataProtectionProvider configured.");
                }

                payload = _provider.Unprotect(container.Payload);
            }

            var item = JsonSerializer.Deserialize<T>(payload, Settings);
            PostProcess(item as RefreshToken);
            return item;
        }

        throw new Exception($"Invalid version in persisted grant data: '{container.PersistentGrantDataContainerVersion}'.");
    }

    private void PostProcess(RefreshToken refreshToken)
    {
        if (refreshToken != null && refreshToken.Version < 5)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var user = new IdentityServerUser(refreshToken.AccessToken.SubjectId);
            if (refreshToken.AccessToken.Claims != null)
            {
                foreach (var claim in refreshToken.AccessToken.Claims)
                {
                    user.AdditionalClaims.Add(claim);
                }
            }

            refreshToken.Subject = user.CreatePrincipal();
            refreshToken.ClientId = refreshToken.AccessToken.ClientId;
            refreshToken.Description = refreshToken.AccessToken.Description;
            refreshToken.AuthorizedScopes = refreshToken.AccessToken.Scopes;
            refreshToken.SetAccessToken(refreshToken.AccessToken);
            refreshToken.AccessToken = null;
            refreshToken.Version = 5;
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}

internal class PersistentGrantDataContainer
{
    public int PersistentGrantDataContainerVersion { get; set; }
    public bool DataProtected { get; set; }
    public string Payload { get; set; }
}
