// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;

namespace Duende.Bff.Yarp.Internal;

internal sealed record RemoteApiConfiguration
{
    /// <summary>
    /// The local path that will be used to access the remote API.
    /// </summary>
    public LocalPath? LocalPath { get; init; }

    /// <summary>
    /// The target URI of the remote API.
    /// </summary>
    public Uri? TargetUri { get; init; }

    /// <summary>
    /// The token requirement for accessing the remote API. Default is <see cref="RequiredTokenType.User"/>.
    /// </summary>
    public RequiredTokenType RequiredTokenType { get; init; } = RequiredTokenType.User;

    /// <summary>
    /// The type name of the access token retriever to use for this remote API.
    /// </summary>
    public string? TokenRetrieverTypeName { get; init; }

    /// <summary>
    /// The parameters for retrieving a user access token.
    /// </summary>
    public UserAccessTokenParameters? UserAccessTokenParameters { get; init; }
}
