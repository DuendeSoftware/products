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
            if (string.IsNullOrEmpty(value) || value == "/")
            {
                // a "/" path is considered the default. So, we'll set it to null to indicate no special matching
                _matchingPath = null;
                return;
            }

            if (!value.StartsWith('/'))
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

    public bool Equals(FrontendMatchingCriteria? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(_matchingPath, other._matchingPath, StringComparison.OrdinalIgnoreCase)
               && Equals(MatchingHostHeader, other.MatchingHostHeader);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(_matchingPath, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(MatchingHostHeader);
        return hashCode.ToHashCode();
    }
}
