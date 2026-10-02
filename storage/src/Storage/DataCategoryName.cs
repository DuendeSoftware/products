// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

/// <summary>
/// Identifies a logical category of storage usage (e.g. configuration, operational data, or
/// dynamic schemas), which is mapped to a <see cref="StorageInstanceId"/> for routing.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
[StringValue]
public partial record DataCategoryName
{
    /// <summary>
    /// The storage category used for dynamic (database-backed) schema storage.
    /// </summary>
    public static DataCategoryName DynamicSchemas { get; } = DataCategoryName.Create("dynamic-schemas");
}
