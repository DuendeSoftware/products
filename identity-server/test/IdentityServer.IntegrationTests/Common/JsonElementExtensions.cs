// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;

namespace IntegrationTests;

public static class JsonElementExtensions
{
    public static T ToObject<T>(this JsonElement element)
    {
        var json = element.GetRawText();
        return JsonSerializer.Deserialize<T>(json);
    }

    public static T ToObject<T>(this JsonDocument document)
    {
        var json = document.RootElement.GetRawText();
        return JsonSerializer.Deserialize<T>(json);
    }

    public static List<string> ToStringList(this JsonElement element) => element.EnumerateArray().Select(item => item.GetString()).ToList();

    public static Dictionary<string, JsonElement> GetFields(this string raw) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(raw);
}
