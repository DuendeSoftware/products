// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Text.Json;
using Duende.Storage.EntityAttributeValue.Internal.Storage;

namespace Duende.IdentityServer.Stores.Storage;

/// <summary>
/// IS-specific helper for extracting string-typed attributes from DSO entries
/// into the <c>Properties</c> dictionaries on runtime models such as
/// <see cref="Duende.IdentityServer.Models.Client.Properties"/>.
/// </summary>
internal static class EavPropertyMapper
{
    /// <summary>
    /// Extracts string-typed attributes as a plain dictionary for populating
    /// runtime model <c>Properties</c> dictionaries.
    /// Returns an empty dictionary when <paramref name="extendedEntries"/> is null or empty.
    /// </summary>
    internal static Dictionary<string, string> ExtractStringProperties(
        IReadOnlyList<AttributeValueDso.V1>? extendedEntries)
    {
        if (extendedEntries is null || extendedEntries.Count == 0)
        {
            return [];
        }

        var result = new Dictionary<string, string>();
        foreach (var e in extendedEntries)
        {
            switch (e.Value)
            {
                case string s:
                    result[e.Name] = s;
                    break;
                case JsonElement { ValueKind: JsonValueKind.String } je:
                    result[e.Name] = je.GetString()!;
                    break;
            }
        }
        return result;
    }
}
