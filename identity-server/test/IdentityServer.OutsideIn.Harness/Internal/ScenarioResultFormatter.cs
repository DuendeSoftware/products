// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>
/// Turns what a scenario returns into something readable before Verify sees it (design doc section 10). This slice
/// handles an <see cref="HttpResponseMessage" />: its status, the headers that describe the response (not transport
/// or run-dependent ones), and its body, with JSON expanded into a tree. Anything else passes through unchanged.
/// </summary>
internal static class ScenarioResultFormatter
{
    // Transport and run-dependent headers that say nothing about IdentityServer's behavior.
    private static readonly HashSet<string> IgnoredHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Content-Length", "Date", "Keep-Alive", "Server", "Transfer-Encoding",
    };

    public static async Task<object?> FormatAsync(object? result, SnapshotScrubber scrubber, TestClock clock, Ct cancellationToken) =>
        result switch
        {
            HttpResponseMessage response => await FormatResponseAsync(response, scrubber, clock, cancellationToken),
            Page page => page.Describe(),
            // A scenario that calls several endpoints itself (for example the token endpoint, then introspection)
            // returns a dictionary naming each step, so every value still goes through this same formatting.
            IReadOnlyDictionary<string, object?> steps => await FormatStepsAsync(steps, scrubber, clock, cancellationToken),
            _ => result,
        };

    private static async Task<Dictionary<string, object?>> FormatStepsAsync(
        IReadOnlyDictionary<string, object?> steps, SnapshotScrubber scrubber, TestClock clock, Ct cancellationToken)
    {
        var formatted = new Dictionary<string, object?>();
        foreach (var (name, value) in steps)
        {
            formatted[name] = await FormatAsync(value, scrubber, clock, cancellationToken);
        }

        return formatted;
    }

    private static async Task<HttpResponseSnapshot> FormatResponseAsync(
        HttpResponseMessage response, SnapshotScrubber scrubber, TestClock clock, Ct cancellationToken)
    {
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .Where(header => !IgnoredHeaders.Contains(header.Key))
            .OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(header => header.Key, header => string.Join(", ", header.Value), StringComparer.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType;

        return new HttpResponseSnapshot(
            $"{(int)response.StatusCode} {response.ReasonPhrase}",
            headers,
            FormatBody(body, mediaType, scrubber, clock));
    }

    private static object? FormatBody(string body, string? mediaType, SnapshotScrubber scrubber, TestClock clock)
    {
        if (body.Length == 0)
        {
            return null;
        }

        if (mediaType is null || !mediaType.EndsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            return body;
        }

        // The tree holds copied strings and numbers, so the document can be disposed once it's built.
        using var document = JsonDocument.Parse(body);
        return ToTree(document.RootElement, propertyName: null, scrubber, clock);
    }

    // propertyName is the JSON field this element was found under (null at the root, or inside an array), so a token
    // or introspection response has its protocol values registered for scrubbing, and its timestamps shown relative to
    // the clock's start, the same way TestBrowser does for a page's query string and claims (design doc section 10).
    private static object? ToTree(JsonElement element, string? propertyName, SnapshotScrubber scrubber, TestClock clock) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => ToTree(property.Value, property.Name, scrubber, clock)),
            JsonValueKind.Array => element.EnumerateArray().Select(item => ToTree(item, propertyName, scrubber, clock)).ToList(),
            JsonValueKind.String => DescribeLeafString(element.GetString()!, propertyName, scrubber, clock),
            JsonValueKind.Number => DescribeLeafNumber(element, propertyName, clock),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

    // Shared with JwtFormatter, so a string or number leaf is handled the same way whether it came from a JSON body
    // directly or from inside a decoded JWT's header or claims (design doc section 10, D25/D26): a well-formed JWS is
    // decoded instead of kept opaque, a registered protocol field becomes a numbered placeholder, and a time field is
    // shown relative to the clock's start.
    internal static object DescribeLeafString(string value, string? propertyName, SnapshotScrubber scrubber, TestClock clock)
    {
        if (JwtFormatter.TryDescribe(value, scrubber, clock, out var decoded))
        {
            return decoded;
        }

        if (propertyName is not null && SnapshotScrubber.ProtocolFields.Contains(propertyName))
        {
            scrubber.RegisterProtocolValue(propertyName, value);
        }

        return value;
    }

    internal static object DescribeLeafNumber(JsonElement element, string? propertyName, TestClock clock) =>
        propertyName is not null && SnapshotScrubber.TimeFields.Contains(propertyName) && element.TryGetInt64(out var seconds)
            ? clock.Describe(DateTimeOffset.FromUnixTimeSeconds(seconds))
            : element.TryGetInt64(out var number) ? number : element.GetDecimal();
}

/// <summary>The readable form of an HTTP response in a snapshot.</summary>
internal sealed record HttpResponseSnapshot(string Status, IReadOnlyDictionary<string, string> Headers, object? Body);
