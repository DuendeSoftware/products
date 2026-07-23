// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Text.Json;

namespace Duende.IdentityServer.Stores.Storage.SamlLogoutSession;

internal static class JsonSamlLogoutSessionSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    internal static string Serialize(Saml.SamlLogoutSession session) =>
        JsonSerializer.Serialize(session, Options);

    internal static Saml.SamlLogoutSession? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Saml.SamlLogoutSession>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
