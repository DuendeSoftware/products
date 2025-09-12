// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace Duende.Bff.Tests.TestInfra;

public static class ResponseExtensions
{
    public static async Task<HttpResponseMessage> CheckHttpStatusCode(this Task<HttpResponseMessage> getResponse,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = await getResponse;
        if (response.StatusCode != statusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Expected {statusCode} but got {response.StatusCode}. Content: {content}");
        }

        return response;
    }

    public static async Task<HttpValidationProblemDetails> ShouldBeProblem(this Task<HttpResponseMessage> getResponse)
    {
        var response = await getResponse;
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        return content ?? throw new InvalidOperationException("no problem details");
    }

    public static async Task<HttpResponseMessage> CheckResponseContent(this Task<HttpResponseMessage> getResponse,
        string value)
    {
        var response = await getResponse;
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe(value);

        return response;
    }

    public static async Task<HttpResponseMessage> CheckResponseContent(this Task<HttpResponseMessage> getResponse,
        byte[] value)
    {
        var response = await getResponse;
        var content = await response.Content.ReadAsByteArrayAsync();
        if (!content.SequenceEqual(value))
        {
            throw new InvalidOperationException("Response content does not match expected value.");
        }

        return response;
    }
}
