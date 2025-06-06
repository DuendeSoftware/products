// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;

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

        return LocalPath.Equals(other.LocalPath) && TargetUri.Equals(other.TargetUri) && RequiredTokenType == other.RequiredTokenType && AccessTokenRetrieverType == other.AccessTokenRetrieverType && Equals(Parameters, other.Parameters);
    }

    public override int GetHashCode() => HashCode.Combine(LocalPath, TargetUri, (int)RequiredTokenType, AccessTokenRetrieverType, Parameters);


    [SetsRequiredMembers]
    public RemoteApi(LocalPath localPath, Uri targetUri)
    {
        LocalPath = localPath;
        TargetUri = targetUri;
        RequiredTokenType = RequiredTokenType.User;
    }

    public required LocalPath LocalPath { get; init; }
    public required Uri TargetUri { get; init; }

    public required RequiredTokenType RequiredTokenType { get; init; }

    public Type? AccessTokenRetrieverType { get; init; }

    public BffUserAccessTokenParameters? Parameters { get; init; }

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
