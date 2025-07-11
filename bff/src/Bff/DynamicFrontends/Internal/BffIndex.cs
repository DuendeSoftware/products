// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class BffIndex
{
    private readonly Dictionary<HostString, PathTrie<BffFrontend>> _perOrigin = new();
    private readonly PathTrie<BffFrontend> _perPath = new();
    private BffFrontend? _defaultFrontend;

    public BffIndex(ILogger logger, FrontendCollection frontends)
    {
        foreach (var frontend in frontends)
        {
            AddFrontend(frontend);
        }
    }

    public void AddFrontend(BffFrontend frontend)
    {
        var frontendSelectionCriteria = frontend.SelectionCriteria;
        if (frontendSelectionCriteria.MatchingOrigin == null)
        {
            if (frontendSelectionCriteria.MatchingPath == null)
            {
                if (_defaultFrontend != null)
                {
                    // Todo: logger.warn
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
            if (!_perOrigin.TryGetValue(frontendSelectionCriteria.MatchingOrigin.ToHostString(), out var trie))
            {
                trie = new PathTrie<BffFrontend>();
                _perOrigin[frontendSelectionCriteria.MatchingOrigin.ToHostString()] = trie;
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
