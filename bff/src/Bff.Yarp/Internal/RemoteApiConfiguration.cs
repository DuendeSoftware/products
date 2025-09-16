// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;

namespace Duende.Bff.Yarp.Internal;

internal sealed record RemoteApiConfiguration
{
    /// <summary>
    /// The local path that will be used to access the remote API.
    /// </summary>
    public string? PathMatch { get; init; }

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

    /// <summary>
    /// How long a request is allowed to remain idle between any operation completing, after which it will be canceled. The default is 100 seconds. The timeout will reset when response headers are received or after successfully reading or writing any request, response, or streaming data like gRPC or WebSockets. TCP keep-alive packets and HTTP/2 protocol pings will not reset the timeout, but WebSocket pings will.
    /// </summary>
    public TimeSpan? ActivityTimeout { get; set; }

    /// <summary>
    /// Allows to use write buffering when sending a response back to the client, if the server hosting YARP (e.g. IIS) supports it. NOTE: enabling it can break SSE (server side event) scenarios.
    /// </summary>
    public bool? AllowResponseBuffering { get; set; }
}
