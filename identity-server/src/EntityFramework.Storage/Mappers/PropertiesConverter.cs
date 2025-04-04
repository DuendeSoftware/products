// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;

namespace Duende.IdentityServer.EntityFramework.Mappers;

internal static class PropertiesConverter
{
    public static string Convert(Dictionary<string, string> sourceMember) => JsonSerializer.Serialize(sourceMember);

    public static Dictionary<string, string> Convert(string sourceMember)
    {
        if (string.IsNullOrWhiteSpace(sourceMember))
        {
            return new Dictionary<string, string>();
        }
        return JsonSerializer.Deserialize<Dictionary<string, string>>(sourceMember);
    }
}
