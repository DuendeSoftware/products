// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.Storage;

namespace Duende.IdentityServer.Admin.IdentityProviders;

/// <summary>
/// Represents the typed storage identifier for an identity provider.
/// </summary>
[ValueOf<Guid>]
public partial record IdentityProviderId
{
    /// <summary>
    /// Creates a new <see cref="IdentityProviderId"/> with a randomly generated UUIDv7 value.
    /// </summary>
    public static IdentityProviderId New() => UuidV7.New().Value;

    internal static bool TryValidate(Guid? input, out IReadOnlyList<string>? errors) => UuidV7.TryValidate(input, out errors);
}
