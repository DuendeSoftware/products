// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace PasskeyAuthentication.TrustPolicies;

/// <summary>
/// Downloads and caches the FIDO Metadata Service blob, extracting the highest
/// certification rank for each authenticator AAGUID.
/// </summary>
public sealed class FidoMdsCache(IHttpClientFactory httpClientFactory, ILogger<FidoMdsCache> logger)
{
    private static readonly Uri MetadataBlobUri = new("https://mds3.fidoalliance.org/");

    private readonly IHttpClientFactory _httpClientFactory =
        httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

    private readonly ILogger<FidoMdsCache> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private Dictionary<Guid, int?>? _entries;

    /// <summary>
    /// Returns a dictionary mapping each AAGUID to its highest FIDO certification rank,
    /// or <c>null</c> if the authenticator has no certification status.
    /// </summary>
    public ValueTask<Dictionary<Guid, int?>> GetEntriesAsync(Ct cancellationToken) =>
        _entries is not null
            ? new ValueTask<Dictionary<Guid, int?>>(_entries)
            : LoadEntriesAsync(cancellationToken);

    private static string DecodeJwtPayload(string jwt)
    {
        // A JWT is three base64url segments: header.payload.signature
        // We extract the payload (middle segment) directly rather than using JwtSecurityTokenHandler
        // because the MDS payload is ~8MB of JSON. JWT libraries parse this through their claims model,
        // which is extremely slow for bulk data. Since we're not validating the signature
        // we can just decode the raw payload and parse the JSON ourselves.
        var segments = jwt.Split('.');
        if (segments.Length < 2 || string.IsNullOrWhiteSpace(segments[1]))
        {
            throw new InvalidOperationException("The FIDO MDS response did not contain a JWT payload.");
        }

        var payloadBytes = Base64Url.DecodeFromChars(segments[1]);
        return Encoding.UTF8.GetString(payloadBytes);
    }

    private static Dictionary<Guid, int?> ParseMetadataEntries(string jwt)
    {
        // In production, you must verify the JWS signature against the FIDO Alliance trust anchors
        // (Root Certificate Authority) before trusting the metadata entries. Without signature validation,
        // a tampered Metadata Service blob could cause this policy to accept compromised authenticators or
        // reject legitimate ones. See: https://fidoalliance.org/specs/mds/fido-metadata-service-v3.0-ps-20210518.html
        var payloadJson = DecodeJwtPayload(jwt);

        using var document = JsonDocument.Parse(payloadJson);
        if (!document.RootElement.TryGetProperty("entries", out var entriesElement) ||
            entriesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The FIDO MDS payload did not contain an 'entries' array.");
        }

        var entries = new Dictionary<Guid, int?>();

        foreach (var entryElement in entriesElement.EnumerateArray())
        {
            if (!entryElement.TryGetProperty("aaguid", out var aaguidElement) ||
                aaguidElement.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(aaguidElement.GetString(), out var aaguid))
            {
                continue;
            }

            int? highestRank = null;

            if (entryElement.TryGetProperty("statusReports", out var statusReportsElement) &&
                statusReportsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var statusReportElement in statusReportsElement.EnumerateArray())
                {
                    if (statusReportElement.TryGetProperty("status", out var statusElement) &&
                        statusElement.ValueKind == JsonValueKind.String &&
                        statusElement.GetString() is { } status &&
                        CertificationLevel.TryGetRank(status, out var rank))
                    {
                        highestRank = Math.Max(highestRank ?? 0, rank);
                    }
                }
            }

            // Merge with any existing entry for the same AAGUID
            if (entries.TryGetValue(aaguid, out var existingRank))
            {
                highestRank = (existingRank, highestRank) switch
                {
                    (null, _) => highestRank,
                    (_, null) => existingRank,
                    _ => Math.Max(existingRank.Value, highestRank.Value),
                };
            }

            entries[aaguid] = highestRank;
        }

        return entries;
    }

    private async ValueTask<Dictionary<Guid, int?>> LoadEntriesAsync(Ct cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient("fido-mds");
        var jwt = await httpClient.GetStringAsync(MetadataBlobUri, cancellationToken);
        _entries = ParseMetadataEntries(jwt);

        _logger.LogInformation("Downloaded FIDO MDS metadata with {EntryCount} AAGUID entries.", _entries.Count);

        return _entries;
    }
}
