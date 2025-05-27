// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class TokenIssueCountDiagnosticEntry : IDiagnosticEntry
{
    private readonly ConcurrentDictionary<string, AtomicCounter> _tokenCounts;
    private readonly MeterListener _meterListener;

    public TokenIssueCountDiagnosticEntry()
    {
        _tokenCounts = new ConcurrentDictionary<string, AtomicCounter>([
            new("Jwt", new AtomicCounter()),
            new ("Reference", new AtomicCounter()),
            new ("Refresh", new AtomicCounter()),
            new("JwtPoPDPoP", new AtomicCounter()),
            new("ReferencePoPDPoP", new AtomicCounter()),
            new("JwtPoPmTLS", new AtomicCounter()),
            new("ReferencePoPmTLS", new AtomicCounter()),
            new("Id", new AtomicCounter())
        ]);
        _meterListener = new MeterListener();

        _meterListener.InstrumentPublished += (instrument, listener) =>
        {
            if (instrument.Name == Telemetry.Metrics.Counters.TokenIssued)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _meterListener.SetMeasurementEventCallback<long>(HandleLongMeasurementRecorded);

        _meterListener.Start();
    }

    public Task WriteAsync(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("TokenIssueCounts");
        writer.WriteStartObject();

        foreach (var (tokenType, counter) in _tokenCounts)
        {
            writer.WriteNumber(tokenType, counter.Count);
        }

        writer.WriteEndObject();

        return Task.CompletedTask;
    }

    private void HandleLongMeasurementRecorded(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (instrument.Name != Telemetry.Metrics.Counters.TokenIssued)
        {
            return;
        }

        var accessTokenIssued = false;
        var accessTokenType = AccessTokenType.Jwt;
        var refreshTokenIssued = false;
        var proofType = ProofType.None;
        var identityTokenIssued = false;
        var grantType = string.Empty;

        foreach (var tag in tags)
        {
            switch (tag.Key)
            {
                case Telemetry.Metrics.Tags.AccessTokenType:
                    if (!Enum.TryParse(tag.Value?.ToString(), out accessTokenType))
                    {
                        accessTokenType = AccessTokenType.Jwt;
                    }
                    break;
                case Telemetry.Metrics.Tags.RefreshTokenIssued:
                    bool.TryParse(tag.Value?.ToString(), out refreshTokenIssued);
                    break;
                case Telemetry.Metrics.Tags.ProofType:
                    if (!Enum.TryParse(tag.Value?.ToString(), out proofType))
                    {
                        proofType = ProofType.None;
                    }
                    break;
                case Telemetry.Metrics.Tags.AccessTokenIssued:
                    bool.TryParse(tag.Value?.ToString(), out accessTokenIssued);
                    break;
                case Telemetry.Metrics.Tags.IdTokenIssued:
                    bool.TryParse(tag.Value?.ToString(), out identityTokenIssued);
                    break;
                case Telemetry.Metrics.Tags.GrantType:
                    grantType = tag.Value?.ToString();
                    break;
            }
        }

        if (accessTokenIssued)
        {
            switch (proofType)
            {
                case ProofType.None when accessTokenType == AccessTokenType.Jwt:
                    _tokenCounts["Jwt"].Increment();
                    break;
                case ProofType.None when accessTokenType == AccessTokenType.Reference:
                    _tokenCounts["Reference"].Increment();
                    break;
                case ProofType.DPoP when accessTokenType == AccessTokenType.Jwt:
                    _tokenCounts["JwtPoPDPoP"].Increment();
                    break;
                case ProofType.DPoP when accessTokenType == AccessTokenType.Reference:
                    _tokenCounts["ReferencePoPDPoP"].Increment();
                    break;
                case ProofType.ClientCertificate when accessTokenType == AccessTokenType.Jwt:
                    _tokenCounts["JwtPoPmTLS"].Increment();
                    break;
                case ProofType.ClientCertificate when accessTokenType == AccessTokenType.Reference:
                    _tokenCounts["ReferencePoPmTLS"].Increment();
                    break;
            }
        }

        if (refreshTokenIssued)
        {
            _tokenCounts["Refresh"].Increment();
        }

        if (identityTokenIssued)
        {
            _tokenCounts["Id"].Increment();
        }

        var tokenWasIssued = accessTokenIssued || refreshTokenIssued || identityTokenIssued;
        if (tokenWasIssued && !string.IsNullOrEmpty(grantType))
        {
            _tokenCounts.AddOrUpdate(grantType, new AtomicCounter(1), (_, counter) =>
            {
                counter.Increment();
                return counter;
            });
        }
    }
}
