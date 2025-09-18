// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace Bff.Benchmarks;

public static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> GetWithCSRF(this HttpClient client, string uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri)
        {
            Headers =
            {
                {"x-csrf", "1"}
            }
        };
        return client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> EnsureStatusCode(this Task<HttpResponseMessage> task, HttpStatusCode? statusCode = HttpStatusCode.OK)
    {
        var response = await task;
        if (response.StatusCode != statusCode)
        {
            throw new HttpRequestException($"Expected status code {statusCode}, but got {response.StatusCode}");
        }
        return response;
    }
}
