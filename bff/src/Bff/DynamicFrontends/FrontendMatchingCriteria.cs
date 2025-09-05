// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.DynamicFrontends;

/// <summary>
/// Determines how a front-end should be matched. 
/// </summary>
public sealed record FrontendMatchingCriteria
{
    private readonly string? _matchingPath;

    /// <summary>
    /// If any matching paths are provided, the frontend will only be selected if the request path matches one of the provided paths.
    /// </summary>
    public string? MatchingPath
    {
        get => _matchingPath;
        init
        {
            if (string.IsNullOrEmpty(value))
            {
                _matchingPath = null;
                return;
            } 

            if (value == "/")
            {
                throw new InvalidOperationException("Path matching on '/' is not allowed");
            }

            if (!value.StartsWith("/"))
            {
                throw new InvalidOperationException("Matching path must start with a '/'");
            }

            _matchingPath = value;
        }
    }

    /// <summary>
    /// If any matching origins are provided, the frontend will only be selected if the request matches one of the provided origins
    /// </summary>
    public HostHeaderValue? MatchingHostHeader { get; init; }

    internal bool HasValue => MatchingHostHeader != null || MatchingPath != null;
}
