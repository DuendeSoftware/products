// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.Storage;

namespace Duende.IdentityServer.Admin.ApiResources;

/// <summary>
/// Represents the typed storage identifier for an API resource.
/// </summary>
[ValueOf<Guid>]
public partial record ApiResourceId
{
    /// <summary>
    /// Creates a new <see cref="ApiResourceId"/> with a randomly generated UUIDv7 value.
    /// </summary>
    public static ApiResourceId New() => UuidV7.New().Value;

    internal static bool TryValidate(Guid? input, out IReadOnlyList<string>? errors) => UuidV7.TryValidate(input, out errors);
}
