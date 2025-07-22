// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Diagnostics.Metrics;
using System.Text.Json;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class TokenIssueCountDiagnosticEntry : IDiagnosticEntry
{
    private long _jwtTokenIssued;
    private long _referenceTokenIssued;
    private long _refreshTokenIssued;
    private long _jwtDPoPTokenIssued;
    private long _referenceDPoPTokenIssued;
    private long _jwtMTLSTokenIssued;
    private long _referenceMTLSTokenIssued;
    private long _idTokenIssued;

    private long _implicitGrantTypeFlows;
    private long _hybridGrantTypeFlows;
    private long _authorizationCodeGrantTypeFlows;
    private long _clientCredentialsGrantTypeFlows;
    private long _resourceOwnerPasswordGrantTypeFlows;
    private long _deviceFlowGrantTypeFlows;
    private long _otherGrantTypeFlows;

    private readonly MeterListener _meterListener;

    public TokenIssueCountDiagnosticEntry()
    {
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

    public Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
    {
        writer.WritePropertyName("TokenIssueCounts");
        writer.WriteStartObject();

        writer.WriteNumber("Jwt", _jwtTokenIssued);
        writer.WriteNumber("Reference", _referenceTokenIssued);
        writer.WriteNumber("JwtDPoP", _jwtDPoPTokenIssued);
        writer.WriteNumber("ReferenceDPoP", _referenceDPoPTokenIssued);
        writer.WriteNumber("JwtMTLS", _jwtMTLSTokenIssued);
        writer.WriteNumber("ReferenceMTLS", _referenceMTLSTokenIssued);
        writer.WriteNumber("Refresh", _refreshTokenIssued);
        writer.WriteNumber("Id", _idTokenIssued);
        writer.WriteNumber(GrantType.Implicit, _implicitGrantTypeFlows);
        writer.WriteNumber(GrantType.Hybrid, _hybridGrantTypeFlows);
        writer.WriteNumber(GrantType.AuthorizationCode, _authorizationCodeGrantTypeFlows);
        writer.WriteNumber(GrantType.ClientCredentials, _clientCredentialsGrantTypeFlows);
        writer.WriteNumber(GrantType.ResourceOwnerPassword, _resourceOwnerPasswordGrantTypeFlows);
        writer.WriteNumber(GrantType.DeviceFlow, _deviceFlowGrantTypeFlows);
        writer.WriteNumber("Other", _otherGrantTypeFlows);

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
                    Interlocked.Increment(ref _jwtTokenIssued);
                    break;
                case ProofType.None when accessTokenType == AccessTokenType.Reference:
                    Interlocked.Increment(ref _referenceTokenIssued);
                    break;
                case ProofType.DPoP when accessTokenType == AccessTokenType.Jwt:
                    Interlocked.Increment(ref _jwtDPoPTokenIssued);
                    break;
                case ProofType.DPoP when accessTokenType == AccessTokenType.Reference:
                    Interlocked.Increment(ref _referenceDPoPTokenIssued);
                    break;
                case ProofType.ClientCertificate when accessTokenType == AccessTokenType.Jwt:
                    Interlocked.Increment(ref _jwtMTLSTokenIssued);
                    break;
                case ProofType.ClientCertificate when accessTokenType == AccessTokenType.Reference:
                    Interlocked.Increment(ref _referenceMTLSTokenIssued);
                    break;
            }
        }

        if (refreshTokenIssued)
        {
            Interlocked.Increment(ref _refreshTokenIssued);
        }

        if (identityTokenIssued)
        {
            Interlocked.Increment(ref _idTokenIssued);
        }

        var tokenWasIssued = accessTokenIssued || refreshTokenIssued || identityTokenIssued;
        if (!tokenWasIssued || string.IsNullOrEmpty(grantType))
        {
            return;
        }

        switch (grantType)
        {
            case GrantType.Implicit:
                Interlocked.Increment(ref _implicitGrantTypeFlows);
                break;
            case GrantType.Hybrid:
                Interlocked.Increment(ref _hybridGrantTypeFlows);
                break;
            case GrantType.AuthorizationCode:
                Interlocked.Increment(ref _authorizationCodeGrantTypeFlows);
                break;
            case GrantType.ClientCredentials:
                Interlocked.Increment(ref _clientCredentialsGrantTypeFlows);
                break;
            case GrantType.ResourceOwnerPassword:
                Interlocked.Increment(ref _resourceOwnerPasswordGrantTypeFlows);
                break;
            case GrantType.DeviceFlow:
                Interlocked.Increment(ref _deviceFlowGrantTypeFlows);
                break;
            default:
                Interlocked.Increment(ref _otherGrantTypeFlows);
                break;
        }
    }
}
