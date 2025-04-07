// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Http;

namespace UnitTests.Common;

/// <summary>
/// An implementation of IHttpContextAccessor that always returns null, to
/// simulate situations where there is no http context.
/// </summary>
internal class NullHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext HttpContext
    {
        get => null;
        set
        {
            // Deliberate no-op
        }
    }
}
