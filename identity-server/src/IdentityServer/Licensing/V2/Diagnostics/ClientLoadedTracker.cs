// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics;

internal class ClientLoadedTracker : IDisposable
{
    private const int MaxClientsTrackedCount = 100;
    private const int ArrayMaxSize = 10;

    private int _clientCount;

    private readonly ConcurrentDictionary<string, JsonObject> _clients = new();
    private readonly List<string> _propertiesToExclude = [nameof(Client.Properties), nameof(Client.LogoUri), nameof(Client.Claims)];
    private readonly JsonDocument _defaultClient;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    public ClientLoadedTracker() => _defaultClient = JsonSerializer.SerializeToDocument(new Client { ClientId = "" }, _serializerOptions);

    public void TrackClientLoaded(Client client)
    {
        if (_clientCount >= MaxClientsTrackedCount)
        {
            return;
        }

        using var clientJson = JsonSerializer.SerializeToDocument(client, _serializerOptions);
        var clientDiagnosticData = new JsonObject();
        foreach (var property in _defaultClient.RootElement.EnumerateObject())
        {
            if (_propertiesToExclude.Contains(property.Name))
            {
                continue;
            }

            if (!_defaultClient.RootElement.TryGetProperty(property.Name, out var defaultValue) ||
                !clientJson.RootElement.TryGetProperty(property.Name, out var clientValue))
            {
                continue;
            }

            if (property.NameEquals(nameof(Client.ClientSecrets)))
            {
                var secrets = clientValue.EnumerateArray()
                    .Select(secret => secret.GetProperty(nameof(Secret.Type)).GetString())
                    .Distinct()
                    .Select(secret => JsonValue.Create(secret))
                    .Cast<JsonNode>();
                clientDiagnosticData["SecretTypes"] = new JsonArray(secrets.ToArray());
            }
            else if (defaultValue.ValueKind == JsonValueKind.Array && clientValue.ValueKind == JsonValueKind.Array && clientValue.GetArrayLength() > 0)
            {
                var arrayEntries = clientValue.EnumerateArray().Take(ArrayMaxSize).Select(CreateJsonValue).Cast<JsonNode>();
                clientDiagnosticData[property.Name] = new JsonArray(arrayEntries.ToArray());
            }
            else
            {
                if (!JsonElementEquals(defaultValue, clientValue))
                {
                    clientDiagnosticData[property.Name] = CreateJsonValue(clientValue);
                }
            }
        }

        if (_clients.ContainsKey(client.ClientId))
        {
            return;
        }

        if (_clients.TryAdd(client.ClientId, clientDiagnosticData))
        {
            _ = Interlocked.Increment(ref _clientCount);
        }
    }

    private static bool JsonElementEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => a.GetDouble().CompareTo(b.GetDouble()) == 0,
            JsonValueKind.True or JsonValueKind.False => a.GetBoolean() == b.GetBoolean(),
            _ => a.ToString() == b.ToString()
        };
    }

    private JsonValue? CreateJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => JsonValue.Create(element.GetString()),
        JsonValueKind.Number => JsonValue.Create(element.GetDouble()),
        JsonValueKind.True or JsonValueKind.False => JsonValue.Create(element.GetBoolean()),
        _ => JsonValue.Create(element.ToString())
    };

    public IReadOnlyDictionary<string, JsonObject> Clients => _clients;

    public void Dispose() => _defaultClient.Dispose();
}
