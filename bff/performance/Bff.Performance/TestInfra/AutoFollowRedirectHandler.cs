// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace Bff.Performance.TestInfra;

public class AutoFollowRedirectHandler(Action<string> writeOutput) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CT ct)
    {
        var previousUri = request.RequestUri;
        for (var i = 0; i < 20; i++)
        {
            var result = await base.SendAsync(request, ct);
            if ((result.StatusCode == HttpStatusCode.Found || result.StatusCode == HttpStatusCode.RedirectKeepVerb) && result.Headers.Location != null)
            {
                writeOutput($"Redirecting from {previousUri} to {result.Headers.Location}");

                var newUri = result.Headers.Location;
                if (!newUri.IsAbsoluteUri)
                {
                    newUri = new Uri(previousUri!, newUri);
                }

                var headers = request.Headers;
                request = new HttpRequestMessage(HttpMethod.Get, newUri);
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }

                previousUri = request.RequestUri;
                continue;
            }

            return result;
        }

        throw new InvalidOperationException("Keeps redirecting forever");
    }
}
