// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Http;

namespace Duende.Bff.Yarp;

public sealed record RemoteApi
{
    public RemoteApi()
    {
    }

    public bool Equals(RemoteApi? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return PathMatch == other.PathMatch
               && TargetUri.Equals(other.TargetUri)
               && RequiredTokenType == other.RequiredTokenType
               && ActivityTimeout == other.ActivityTimeout
               && AllowResponseBuffering == other.AllowResponseBuffering
               && AccessTokenRetrieverType == other.AccessTokenRetrieverType
               && Equals(Parameters, other.Parameters);
    }

    public override int GetHashCode() => HashCode.Combine(PathMatch, TargetUri, (int)RequiredTokenType, AccessTokenRetrieverType, Parameters, ActivityTimeout, AllowResponseBuffering);

    [SetsRequiredMembers]
    public RemoteApi(PathString pathMatch, Uri targetUri)
    {
        ArgumentNullException.ThrowIfNull(targetUri);
        if (pathMatch.Value != null && !pathMatch.Value.StartsWith('/'))
        {
            throw new ArgumentException("Path must start with '/'", nameof(pathMatch));
        }

        if (!targetUri.IsAbsoluteUri)
        {
            throw new ArgumentException("Target must be absolute Uri", nameof(targetUri));
        }

        PathMatch = pathMatch;
        TargetUri = targetUri;
        RequiredTokenType = RequiredTokenType.User;
    }

    public required PathString PathMatch { get; init; }

    public required Uri TargetUri { get; init; }

    public required RequiredTokenType RequiredTokenType { get; init; }

    public Type? AccessTokenRetrieverType { get; init; }

    public BffUserAccessTokenParameters? Parameters { get; init; }

    public TimeSpan? ActivityTimeout { get; init; }

    public RemoteApi WithActivityTimeout(TimeSpan timeout) => this with
    {
        ActivityTimeout = timeout
    };

    public bool? AllowResponseBuffering { get; init; }

    public RemoteApi WithResponseBufferingAllowed(bool allow) => this with
    {
        AllowResponseBuffering = allow
    };

    public RemoteApi WithAccessToken(RequiredTokenType type) => this with
    {
        RequiredTokenType = type
    };

    public RemoteApi WithAccessTokenRetriever<TRetriever>() where TRetriever : IAccessTokenRetriever => this with
    {
        AccessTokenRetrieverType = typeof(TRetriever)
    };

    public RemoteApi WithUserAccessTokenParameters(BffUserAccessTokenParameters parameters) => this with
    {
        Parameters = parameters
    };
}
