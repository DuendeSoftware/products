// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;

namespace Duende.IdentityServer.Services.KeyManagement;

internal static class KeySerializer
{
    private static JsonSerializerOptions _settings =
        new()
        {
            IncludeFields = true
        };

    public static string Serialize<T>(T item) => JsonSerializer.Serialize(item, item.GetType(), _settings);

    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _settings);
}
