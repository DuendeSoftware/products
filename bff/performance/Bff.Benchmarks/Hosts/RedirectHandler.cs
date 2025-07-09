// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace Bff.Benchmarks.Hosts;

internal class RedirectHandler() : DelegatingHandler
{
    private const int MaxRedirects = 20;

    public bool AutoFollowRedirects { get; set; } = true;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CT ct)
    {
        var originalUri = request.RequestUri;

        for (var redirectCount = 0; redirectCount < MaxRedirects; redirectCount++)
        {
            var response = await base.SendAsync(request, ct);

            if (!AutoFollowRedirects)
            {
                return response;
            }

            if (response.StatusCode == HttpStatusCode.Found && response.Headers.Location != null)
            {
                var newUri = response.Headers.Location;
                if (!newUri.IsAbsoluteUri)
                {
                    newUri = new Uri(originalUri!, newUri);
                }

                var headers = request.Headers;
                request = new HttpRequestMessage(HttpMethod.Get, newUri);
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }

                originalUri = request.RequestUri;
                continue;
            }

            return response;
        }

        throw new InvalidOperationException($"Should have redirected within {MaxRedirects} attempts");
    }
}
