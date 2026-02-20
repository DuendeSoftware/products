// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Bff.Performance.TestInfra;

public class RequestLoggingHandler(
    ILogger<RequestLoggingHandler> log,
    Func<HttpRequestMessage, bool> shouldLog)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CT ct)
    {
        if (!shouldLog(request))
        {
            return await base.SendAsync(request, ct);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await base.SendAsync(request, ct);

            log.LogInformation("Executing {method} on {url} returned {statuscode} in {ms} ms",
                request.Method,
                request.RequestUri,
                result.StatusCode,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            log.LogWarning("Executing {method} on {url} was cancelled in {ms} ms",
                request.Method,
                request.RequestUri,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "Exception while executing {method} on {url} in {ms} ms",
                request.Method,
                request.RequestUri,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
