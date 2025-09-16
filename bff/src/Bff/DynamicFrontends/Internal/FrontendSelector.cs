// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class FrontendSelector
{
    private readonly FrontendCollection _frontends;
    private readonly ILogger<FrontendSelector> _logger;
    BffIndex _bffIndex;


    public FrontendSelector(FrontendCollection frontends, ILogger<FrontendSelector> logger)
    {
        _frontends = frontends;
        _logger = logger;
        _bffIndex = new BffIndex(logger, frontends);

        _frontends.OnFrontendChanged += (_) =>
        {
            _bffIndex = new BffIndex(logger, _frontends);
        };

        _frontends.OnFrontendAdded += frontend =>
        {
            _bffIndex.AddFrontend(frontend);
        };
    }


    public bool TrySelectFrontend(HttpRequest request, [NotNullWhen(true)] out BffFrontend? selectedFrontend)
    {
        selectedFrontend = null;

        if (_frontends.Count == 0)
        {
            _logger.NoFrontendSelected(LogLevel.Debug);
            return false;
        }

        return _bffIndex.TryMatch(request, out selectedFrontend);
    }
}
