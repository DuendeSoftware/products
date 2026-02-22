// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Blazor.Client.Internals;

internal class AntiForgeryHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        Ct ct)
    {
        request.Headers.Add("X-CSRF", "1");
        return base.SendAsync(request, ct);
    }
}
