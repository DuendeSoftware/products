// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Duende.Bff;

/// <summary>
/// ActivityPropagationHandler propagates the current Activity to the downstream service
/// </summary>
public sealed class ActivityPropagationHandler : DelegatingHandler
{
    private const string RequestIdHeaderName = "Request-Id";
    private const string BaggageHeaderName = "Correlation-Context";
    private const string TraceParentHeaderName = "traceparent";
    private const string TraceStateHeaderName = "tracestate";


    /// <inheritdoc />
    public ActivityPropagationHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }
        
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var currentActivity = Activity.Current;
        if (currentActivity is not null)
        {
            InjectHeaders(currentActivity, request.Headers);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static void InjectHeaders(Activity activity, HttpHeaders headers)
    {
        if (activity.IdFormat == ActivityIdFormat.W3C)
        {
            headers.Remove(TraceParentHeaderName);
            headers.Remove(TraceStateHeaderName);

            headers.TryAddWithoutValidation(TraceParentHeaderName, activity.Id);
            if (activity.TraceStateString != null)
            {
                headers.TryAddWithoutValidation(TraceStateHeaderName, activity.TraceStateString);
            }
        }
        else
        {
            headers.Remove(RequestIdHeaderName);
            headers.TryAddWithoutValidation(RequestIdHeaderName, activity.Id);
        }

        // we expect baggage to be empty or contain a few items
        using var e = activity.Baggage.GetEnumerator();
        if (e.MoveNext())
        {
            var baggage = new StringBuilder();
            do
            {
                var item = e.Current;
                baggage.Append(Uri.EscapeDataString(item.Key));
                baggage.Append('=');
                baggage.Append(Uri.EscapeDataString(item.Value ?? string.Empty));
                baggage.Append(", ");
            }
            while (e.MoveNext());

            baggage.Length -= 2; // Account for the last ", "

            headers.Remove(BaggageHeaderName);
            headers.TryAddWithoutValidation(BaggageHeaderName, baggage.ToString());
        }
    }
}