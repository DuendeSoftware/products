// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class FrontendSelector(FrontendCollection frontendCollection, ILogger<FrontendSelector> logger)
{
    public bool TrySelectFrontend(HttpRequest request, [NotNullWhen(true)] out BffFrontend? selectedFrontend)
    {
        selectedFrontend = null;
        var frontends = frontendCollection.GetAll();

        if (frontends.Count == 0)
        {
            logger.NoFrontendSelected(LogLevel.Debug);
            return false;
        }

        // Find frontends that match the request by origin (if specified)
        var matchingByOrigin = frontends
            .Where(x => x.SelectionCriteria.MatchingOrigin == null || x.SelectionCriteria.MatchingOrigin.Equals(request))
            .ToList();

        // First, look for a match by origin and path (if specified)
        selectedFrontend = matchingByOrigin
            .OrderByDescending(x => x.SelectionCriteria.MatchingOrigin != null) // Prefer frontends with a specific origin
            .ThenByDescending(x => PathOrder(x.SelectionCriteria.MatchingPath))
            .FirstOrDefault(x =>
                x.SelectionCriteria.MatchingPath == null ||
                PathMatches(request, x.SelectionCriteria.MatchingPath)
            );

        if (selectedFrontend != null)
        {
            if (selectedFrontend.SelectionCriteria.MatchingPath != null
                && !request.Path.StartsWithSegments(selectedFrontend.SelectionCriteria.MatchingPath,
                    StringComparison.Ordinal))
            {
                // There is a case difference in the path
                logger.FrontendSelectedWithPathCasingIssue(LogLevel.Warning, selectedFrontend.SelectionCriteria.MatchingPath, request.Path);
            }

            return true;
        }

        // Fallback: match any frontend without origin or path restrictions
        selectedFrontend = frontends.FirstOrDefault(x =>
            x.SelectionCriteria.MatchingOrigin == null &&
            x.SelectionCriteria.MatchingPath == null
        );

        return selectedFrontend != null;
    }

    private bool PathMatches(HttpRequest request, PathString path) =>
        request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase);

    private int PathOrder(PathString? path) =>
        path?.Value?.Length ?? 0;
}
