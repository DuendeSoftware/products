// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;

namespace Duende.IdentityServer.Services.KeyManagement;

internal static class KeySerializer
{
    private static JsonSerializerOptions _settings =
        new JsonSerializerOptions
        {
            IncludeFields = true
        };

    public static string Serialize<T>(T item)
    {
        return JsonSerializer.Serialize(item, item.GetType(), _settings);
    }

    public static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, _settings);
    }
}
