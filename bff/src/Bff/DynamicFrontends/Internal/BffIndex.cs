// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class BffIndex
{
    private readonly ILogger _logger;
    private readonly Dictionary<HostString, PathTrie<BffFrontend>> _perOrigin = new();
    private readonly PathTrie<BffFrontend> _perPath = new();
    private BffFrontend? _defaultFrontend;
    private readonly Dictionary<FrontendMatchingCriteria, BffFrontendName> _registeredCriteria = new();
    public BffIndex(ILogger logger, FrontendCollection frontends)
    {
        _logger = logger;
        foreach (var frontend in frontends)
        {
            AddFrontend(frontend);
        }
    }

    public void AddFrontend(BffFrontend frontend)
    {
        var frontendSelectionCriteria = frontend.MatchingCriteria;

        if (!_registeredCriteria.TryAdd(frontendSelectionCriteria, frontend.Name))
        {
            _registeredCriteria.TryGetValue(frontendSelectionCriteria, out var collidesWith);
            _logger.FrontendWithSimilarMatchingCriteriaAlreadyRegistered(LogLevel.Warning,
                frontend.Name,
                collidesWith
            );
            return;
        }

        if (frontendSelectionCriteria.MatchingHostHeader == null)
        {
            if (frontendSelectionCriteria.MatchingPath == null)
            {
                if (_defaultFrontend != null)
                {
                    // This should no longer happen.
                    _logger.DuplicateDefaultRouteConfigured(LogLevel.Warning);
                    return;
                }

                _defaultFrontend = frontend;
            }
            else
            {
                _perPath.Add(frontendSelectionCriteria.MatchingPath, frontend);
            }
        }
        else
        {
            if (!_perOrigin.TryGetValue(frontendSelectionCriteria.MatchingHostHeader.ToHostString(), out var trie))
            {
                trie = new PathTrie<BffFrontend>();
                _perOrigin[frontendSelectionCriteria.MatchingHostHeader.ToHostString()] = trie;
            }

            var matchingPath = frontendSelectionCriteria.MatchingPath;
            if (matchingPath == null)
            {
                matchingPath = "/";
            }
            trie.Add(matchingPath, frontend);
        }
    }

    public bool TryMatch(HttpRequest request, [NotNullWhen(true)] out BffFrontend? match)
    {
        var requestHost = request.Host;

        // We don't always have the port in the request, so we need to ensure we have a complete HostString.
        if (requestHost.Port == null)
        {
            requestHost = new HostString(requestHost.Host, request.IsHttps ? 443 : 80);
        }

        var trie = _perOrigin
            .FirstOrDefault(x => x.Key.Equals(requestHost))
            .Value;

        if (trie == null)
        {
            trie = _perPath;
        }

        var result = trie.Match(request.Path);
        if (result.HasMatch)
        {
            match = result.Value;
            return true;
        }

        match = _defaultFrontend;
        return _defaultFrontend != null;
    }
}
