// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Diagnostics.Metrics;
using System.Text.Json;
using Duende.IdentityServer.Extensions;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class EndpointUsageDiagnosticEntry : IDiagnosticEntry
{
    private long _authorizeCallback;
    private long _authorize;
    private long _backChannelAuthentication;
    private long _checkSession;
    private long _deviceAuthorization;
    private long _discoveryKey;
    private long _discovery;
    private long _endSessionCallback;
    private long _endSession;
    private long _introspection;
    private long _par;
    private long _tokenRevocation;
    private long _token;
    private long _userInfo;
    private long _other;

    private readonly MeterListener _meterListener;

    public EndpointUsageDiagnosticEntry()
    {
        _meterListener = new MeterListener();

        _meterListener.InstrumentPublished += (instrument, listener) =>
        {
            if (instrument.Name == Telemetry.Metrics.Counters.ActiveRequests)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _meterListener.SetMeasurementEventCallback<long>(MeasurementCallback);

        _meterListener.Start();
    }

    public Task WriteAsync(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("EndpointUsage");

        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.AuthorizeCallback.EnsureLeadingSlash(), _authorizeCallback);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.Authorize.EnsureLeadingSlash(), _authorize);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.BackchannelAuthentication.EnsureLeadingSlash(), _backChannelAuthentication);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.CheckSession.EnsureLeadingSlash(), _checkSession);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.DeviceAuthorization.EnsureLeadingSlash(), _deviceAuthorization);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.DiscoveryWebKeys.EnsureLeadingSlash(), _discoveryKey);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.DiscoveryConfiguration.EnsureLeadingSlash(), _discovery);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.EndSessionCallback.EnsureLeadingSlash(), _endSessionCallback);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.EndSession.EnsureLeadingSlash(), _endSession);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.Introspection.EnsureLeadingSlash(), _introspection);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.PushedAuthorization.EnsureLeadingSlash(), _par);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.Revocation.EnsureLeadingSlash(), _tokenRevocation);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.Token.EnsureLeadingSlash(), _token);
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.UserInfo.EnsureLeadingSlash(), _userInfo);
        writer.WriteNumber("other", _other);

        writer.WriteEndObject();

        return Task.CompletedTask;
    }

    private void MeasurementCallback(Instrument instrument, long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        var isIncrementedRequest = instrument.Name == Telemetry.Metrics.Counters.ActiveRequests && measurement == 1;
        if (!isIncrementedRequest)
        {
            return;
        }

        string? endpointName = null;
        foreach (var tag in tags)
        {
            if (tag.Key != Telemetry.Metrics.Tags.Path)
            {
                continue;
            }

            endpointName = tag.Value?.ToString();
            break;
        }

        switch (endpointName.RemoveLeadingSlash())
        {
            case IdentityServerConstants.ProtocolRoutePaths.AuthorizeCallback:
                Interlocked.Increment(ref _authorizeCallback);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.Authorize:
                Interlocked.Increment(ref _authorize);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.BackchannelAuthentication:
                Interlocked.Increment(ref _backChannelAuthentication);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.CheckSession:
                Interlocked.Increment(ref _checkSession);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.DeviceAuthorization:
                Interlocked.Increment(ref _deviceAuthorization);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.DiscoveryWebKeys:
                Interlocked.Increment(ref _discoveryKey);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.DiscoveryConfiguration:
                Interlocked.Increment(ref _discovery);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.EndSessionCallback:
                Interlocked.Increment(ref _endSessionCallback);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.EndSession:
                Interlocked.Increment(ref _endSession);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.Introspection:
                Interlocked.Increment(ref _introspection);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.PushedAuthorization:
                Interlocked.Increment(ref _par);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.Revocation:
                Interlocked.Increment(ref _tokenRevocation);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.Token:
                Interlocked.Increment(ref _token);
                break;
            case IdentityServerConstants.ProtocolRoutePaths.UserInfo:
                Interlocked.Increment(ref _userInfo);
                break;
            default:
                Interlocked.Increment(ref _other);
                break;
        }
    }
}
