// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>
/// Decodes a JWT into its header and claims before Verify sees it (design doc section 10, D25/D26), so a snapshot
/// shows what a token actually carries instead of an opaque placeholder. Shared by
/// <see cref="ScenarioResultFormatter" /> (back-channel token and introspection response bodies) and
/// <see cref="TestBrowser" /> (the journal, for example a form_post's id_token, and id_token_hint on a logout URL), so
/// a JWT reads the same wherever it shows up.
/// </summary>
internal static class JwtFormatter
{
    /// <summary>
    /// Describes <paramref name="value" /> as its header and claims, without the signature, if it's a well-formed JWS:
    /// three base64url segments, with a header that parses as JSON and carries an "alg". A JWE (five segments), an
    /// opaque value (a reference token, a refresh token, a code), or anything else that doesn't decode leaves
    /// <paramref name="decoded" /> unset and returns false, so the caller falls back to its usual placeholder handling.
    /// </summary>
    public static bool TryDescribe(string value, SnapshotScrubber scrubber, TestClock clock, out object decoded)
    {
        decoded = null!;
        if (!TryDecode(value, out var header, out var claims))
        {
            return false;
        }

        decoded = new Dictionary<string, object?>
        {
            ["Header"] = ToTree(header, scrubber, clock),
            ["Claims"] = ToTree(claims, scrubber, clock),
        };
        return true;
    }

    private static bool TryDecode(string value, out JsonElement header, out JsonElement claims)
    {
        header = default;
        claims = default;

        var segments = value.Split('.');
        if (segments.Length != 3 || Array.Exists(segments, segment => segment.Length == 0))
        {
            // A JWE has five segments; anything else with the wrong shape is an opaque value, not a JWT.
            return false;
        }

        if (!TryDecodeSegment(segments[0], out var headerJson) || !TryDecodeSegment(segments[1], out var claimsJson))
        {
            return false;
        }

        try
        {
            using var headerDocument = JsonDocument.Parse(headerJson);
            if (headerDocument.RootElement.ValueKind != JsonValueKind.Object
                || !headerDocument.RootElement.TryGetProperty("alg", out var alg)
                || alg.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            using var claimsDocument = JsonDocument.Parse(claimsJson);
            if (claimsDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            header = headerDocument.RootElement.Clone();
            claims = claimsDocument.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryDecodeSegment(string segment, out string json)
    {
        json = "";
        try
        {
            var padded = segment.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - (padded.Length % 4)) % 4);
            json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Header and claim names come out sorted, so the same token renders identically regardless of the order the
    // issuer wrote its fields in. Leaf values go through the same scrubbing and time handling as a JSON response body
    // (ScenarioResultFormatter's leaf helpers), so a registered issuer, user, client or protocol value is recognized
    // here too.
    private static object? ToTree(JsonElement element, SnapshotScrubber scrubber, TestClock clock, string? propertyName = null) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToDictionary(property => property.Name, property => ToTree(property.Value, scrubber, clock, property.Name)),
            JsonValueKind.Array => element.EnumerateArray().Select(item => ToTree(item, scrubber, clock, propertyName)).ToList(),
            JsonValueKind.String => ScenarioResultFormatter.DescribeLeafString(element.GetString()!, propertyName, scrubber, clock),
            JsonValueKind.Number => ScenarioResultFormatter.DescribeLeafNumber(element, propertyName, clock),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
}
