// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Tests.TestInfra;

internal class FakeHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = new DefaultHttpContext();
}
