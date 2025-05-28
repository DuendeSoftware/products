// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Duende.IdentityServer.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class ClientInfoDiagnosticEntry : IDiagnosticEntry
{
    private static readonly RemovePropertyModifier<Client> RemoveSecretsModifier = new(
    [
        nameof(Client.ClientSecrets)
    ]);
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { RemoveSecretsModifier.ModifyTypeInfo }
        },
        WriteIndented = false
    };

    private readonly IClientStore _clientStore;
    private readonly ILogger<ClientInfoDiagnosticEntry> _logger;
    private readonly ConcurrentDictionary<string, Client> _clients = new();
    private readonly MeterListener _meterListener;

    public ClientInfoDiagnosticEntry(IClientStore clientStore, ILogger<ClientInfoDiagnosticEntry> logger)
    {
        _clientStore = clientStore;
        _logger = logger;
        _meterListener = new MeterListener();

        _meterListener.InstrumentPublished += (instrument, listener) =>
        {
            if (instrument.Name == Telemetry.Metrics.Counters.ClientLoaded)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _meterListener.SetMeasurementEventCallback<long>(HandleClientMeasurementRecorded);

        _meterListener.Start();
    }

    public Task WriteAsync(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("Clients");

        foreach (var (clientId, client) in _clients)
        {
            writer.WritePropertyName(clientId);
            JsonSerializer.Serialize(writer, client, _serializerOptions);
        }

        writer.WriteEndObject();

        return Task.CompletedTask;
    }

    private void HandleClientMeasurementRecorded(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (instrument.Name != Telemetry.Metrics.Counters.ClientLoaded)
        {
            return;
        }

        string? clientId = null;
        foreach (var tag in tags)
        {
            if (tag is { Key: Telemetry.Metrics.Tags.Client, Value: string id })
            {
                clientId = id;
            }
        }

        if (clientId != null && !_clients.ContainsKey(clientId))
        {
            _ = LoadClientAsync(clientId);
        }
    }

    private async Task LoadClientAsync(string clientId)
    {
        try
        {
            if (_clients.ContainsKey(clientId))
            {
                return;
            }

            //It's important to use FindClientByIdAsync here since we are responding to a measurement event
            //which is triggered by the use of FindEnabledClientByIdAsync. We also do not care if the client
            //is enabled or not at this point. If it was loaded, we want to see it in the diagnostics.
            var client = await _clientStore.FindClientByIdAsync(clientId);
            if (client != null)
            {
                _clients.TryAdd(clientId, client);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding client {ClientId} to diagnostics", clientId);
        }
    }
}
