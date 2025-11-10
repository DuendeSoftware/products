// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends;

public sealed class CurrentFrontendAccessor(IHttpContextAccessor contextAccessor)
{
    private const string HttpItemName = "Duende.Bff.Frontend";

    internal void Set(BffFrontend frontend)
    {
        if (contextAccessor.HttpContext == null)
        {
            throw new InvalidOperationException("Not running in a http context");
        }

        contextAccessor.HttpContext.Items[HttpItemName] = frontend;
    }

    public bool TryGet([NotNullWhen(true)] out BffFrontend? frontend)
    {
        if (contextAccessor.HttpContext == null)
        {
            frontend = null;
            return false;
        }

        contextAccessor.HttpContext.Items.TryGetValue(HttpItemName, out var value);
        frontend = value as BffFrontend;
        return frontend != null;
    }

    public BffFrontend Get()
    {
        if (!TryGet(out var frontend))
        {
            throw new InvalidOperationException("Frontend not found"); //TODO improve exception message
        }

        return frontend;
    }
}
