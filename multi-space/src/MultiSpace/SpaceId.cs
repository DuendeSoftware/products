// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.MultiSpace;

/// <summary>
/// Represents a space identifier for multi-tenancy support.
/// </summary>
[ValueOf<Guid>]
public partial record SpaceId
{
    /// <summary>
    /// Gets the management space ID, used for cross-space management operations.
    /// </summary>
    public static SpaceId Management { get; } = new(Guid.Parse("00000000-0000-7000-8000-000000000001"));

    /// <summary>
    /// Gets the default space ID, used for single-tenant scenarios.
    /// </summary>
    public static SpaceId Default { get; } = new(Guid.Parse("00000000-0000-7000-8000-000000000002"));

    /// <summary>
    /// Creates a new space ID using UUIDv7.
    /// </summary>
    public static SpaceId New() => UuidV7.New().Value;

    internal static bool TryValidate(Guid? input, out IReadOnlyList<string>? errors) => UuidV7.TryValidate(input, out errors);
}
