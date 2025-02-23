using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Hosts.Tests.TestInfra;

public class RequestLoggingHandler(
    ILogger<RequestLoggingHandler> log,
    Func<HttpRequestMessage, bool> shouldLog)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!shouldLog(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await base.SendAsync(request, cancellationToken);

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