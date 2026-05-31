// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Diagnostics.Metrics;
using System.Text.Json;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Saml;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class EndpointUsageDiagnosticEntry : IDiagnosticEntry, IDisposable
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
    private long _oAuthMetadata;
    private long _samlMetadata;
    private long _samlSignIn;
    private long _samlSignInCallback;
    private long _samlLogout;
    private long _samlLogoutCallback;
    private long _samlSpLogoutCompletion;
    private long _other;

    private readonly MeterListener _meterListener;
    private readonly string _samlMetadataPath;
    private readonly string _samlSignInPath;
    private readonly string _samlSignInCallbackPath;
    private readonly string _samlLogoutPath;
    private readonly string _samlLogoutCallbackPath;

    public EndpointUsageDiagnosticEntry(IOptions<IdentityServerOptions> identityServerOptions)
    {
        var samlOptions = identityServerOptions.Value.Saml;
        var samlEndpoints = samlOptions.Endpoints;
        _samlMetadataPath = EndpointHelpers.SamlMetadataHelpers.ResolveMetadataPath(samlOptions).EnsureLeadingSlash();
        _samlSignInPath = samlEndpoints.SingleSignOnServicePath.EnsureLeadingSlash();
        _samlSignInCallbackPath = samlEndpoints.SingleSignOnCallbackPath.EnsureLeadingSlash();
        _samlLogoutPath = samlEndpoints.SingleLogoutServicePath.EnsureLeadingSlash();
        _samlLogoutCallbackPath = samlEndpoints.SingleLogoutCallbackPath.EnsureLeadingSlash();

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

    public Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
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
        writer.WriteNumber(IdentityServerConstants.ProtocolRoutePaths.OAuthMetadata.EnsureLeadingSlash(), _oAuthMetadata);
        writer.WriteNumber(_samlMetadataPath, _samlMetadata);
        writer.WriteNumber(_samlSignInPath, _samlSignIn);
        writer.WriteNumber(_samlSignInCallbackPath, _samlSignInCallback);
        writer.WriteNumber(_samlLogoutPath, _samlLogout);
        writer.WriteNumber(_samlLogoutCallbackPath, _samlLogoutCallback);
        writer.WriteNumber(SamlConstants.Defaults.SpLogoutCompletionPath.EnsureLeadingSlash(), _samlSpLogoutCompletion);
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
            //NOTE: need to use StartsWith because this route can have additional segments
            case { } s when s.StartsWith(IdentityServerConstants.ProtocolRoutePaths.OAuthMetadata, StringComparison.OrdinalIgnoreCase):
                Interlocked.Increment(ref _oAuthMetadata);
                break;
            case { } s when s.Equals(_samlLogoutPath.TrimStart('/'), StringComparison.OrdinalIgnoreCase):
                Interlocked.Increment(ref _samlLogout);
                break;
            case { } s when s.Equals(_samlLogoutCallbackPath.TrimStart('/'), StringComparison.OrdinalIgnoreCase):
                Interlocked.Increment(ref _samlLogoutCallback);
                break;
            case { } s when s.Equals(_samlMetadataPath.TrimStart('/'), StringComparison.OrdinalIgnoreCase):
                Interlocked.Increment(ref _samlMetadata);
                break;
            case { } s when s.Equals(_samlSignInPath.TrimStart('/'), StringComparison.OrdinalIgnoreCase):
                Interlocked.Increment(ref _samlSignIn);
                break;
            case { } s when s.Equals(_samlSignInCallbackPath.TrimStart('/'), StringComparison.OrdinalIgnoreCase):
                Interlocked.Increment(ref _samlSignInCallback);
                break;
            case { } s when s.Equals(SamlConstants.Defaults.SpLogoutCompletionPath.TrimStart('/'), StringComparison.OrdinalIgnoreCase):
                Interlocked.Increment(ref _samlSpLogoutCompletion);
                break;
            default:
                Interlocked.Increment(ref _other);
                break;
        }
    }

    public void Dispose() => _meterListener.Dispose();
}
