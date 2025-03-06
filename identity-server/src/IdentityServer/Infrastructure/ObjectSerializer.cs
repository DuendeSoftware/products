// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.IdentityServer;

internal static class ObjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ToString(object o)
    {
        return JsonSerializer.Serialize(o, Options);
    }

    public static T FromString<T>(string value)
    {
        return JsonSerializer.Deserialize<T>(value, Options);
    }
}