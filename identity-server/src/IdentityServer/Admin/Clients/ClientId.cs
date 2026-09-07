// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.Storage;

namespace Duende.IdentityServer.Admin.Clients;

/// <summary>
/// Represents the typed storage identifier for a client.
/// </summary>
[ValueOf<Guid>]
public partial record ClientId
{
    /// <summary>
    /// Creates a new <see cref="ClientId"/> with a randomly generated UUIDv7 value.
    /// </summary>
    public static ClientId New() => UuidV7.New().Value;

    internal static bool TryValidate(Guid? input, out IReadOnlyList<string>? errors) => UuidV7.TryValidate(input, out errors);
}
