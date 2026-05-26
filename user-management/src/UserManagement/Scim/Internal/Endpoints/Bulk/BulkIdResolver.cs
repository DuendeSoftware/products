// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Scim.Internal.Endpoints.Bulk;

/// <summary>
/// Tracks resolved <c>bulkId</c> → resource ID mappings and substitutes
/// <c>bulkId:xxx</c> references in operation paths and raw JSON text on demand.
/// </summary>
internal sealed class BulkIdResolver
{
    internal const string BulkIdPrefix = "bulkId:";

    private readonly Dictionary<string, string> _resolved =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a resolved mapping from a client-assigned <paramref name="bulkId"/>
    /// to the server-assigned <paramref name="resourceId"/>.
    /// </summary>
    internal void Register(string bulkId, string resourceId) =>
        _resolved[bulkId] = resourceId;

    /// <summary>
    /// Resolves any <c>bulkId:xxx</c> reference in a path segment.
    /// Returns <c>false</c> if the path contains an unresolved reference.
    /// </summary>
    internal bool TryResolvePath(ref string path)
    {
        var idx = path.IndexOf(BulkIdPrefix, StringComparison.Ordinal);
        if (idx < 0)
        {
            return true;
        }

        var afterPrefix = path[(idx + BulkIdPrefix.Length)..];

        var slashIdx = afterPrefix.IndexOf('/', StringComparison.Ordinal);
        var bulkId = slashIdx >= 0 ? afterPrefix[..slashIdx] : afterPrefix;

        if (!_resolved.TryGetValue(bulkId, out var resourceId))
        {
            return false;
        }

        path = path.Replace(BulkIdPrefix + bulkId, resourceId, StringComparison.Ordinal);
        return true;
    }

    /// <summary>
    /// Replaces all <c>bulkId:xxx</c> references in the given raw JSON text
    /// with their resolved resource IDs.
    /// Returns <c>null</c> if any reference cannot be resolved.
    /// </summary>
    internal string? ResolveJsonText(string rawJson)
    {
        var result = rawJson;
        var searchStart = 0;

        while (true)
        {
            var idx = result.IndexOf(BulkIdPrefix, searchStart, StringComparison.Ordinal);
            if (idx < 0)
            {
                return result;
            }

            var keyStart = idx + BulkIdPrefix.Length;
            var keyEnd = keyStart;
            while (keyEnd < result.Length && result[keyEnd] is not '"' and not ',' and not '}' and not ']' and not ' ')
            {
                keyEnd++;
            }

            var bulkId = result[keyStart..keyEnd];
            if (!_resolved.TryGetValue(bulkId, out var resourceId))
            {
                return null;
            }

            var token = BulkIdPrefix + bulkId;
            result = string.Concat(result.AsSpan(0, idx), resourceId, result.AsSpan(idx + token.Length));
            searchStart = idx + resourceId.Length;
        }
    }

    /// <summary>Returns the bulkId key from a "bulkId:xxx" value, or null if not a reference.</summary>
    internal static string? GetBulkIdKey(string value) =>
        value.StartsWith(BulkIdPrefix, StringComparison.Ordinal)
            ? value[BulkIdPrefix.Length..]
            : null;
}
