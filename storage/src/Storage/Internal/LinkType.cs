// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// The type of link between two entities.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <param name="Id">A number representation for the link type.</param>
/// <param name="Name">The name of the link type. This name is only used for display purposes and should never change.</param>
public readonly record struct LinkType(uint Id, string Name)
{
    /// <summary>
    /// Obsolete parameterless constructor. Do not use.
    /// </summary>
    [Obsolete("Don't use this constructor")]
    public LinkType() : this(0!, null!) => throw new InvalidOperationException("Cannot instantiate LinkType without parameters");

    /// <summary>
    /// Converts a <see cref="LinkTypeRegistry"/> value to a <see cref="LinkType"/>.
    /// </summary>
    /// <param name="registry">The registry value to convert.</param>
    /// <returns>A <see cref="LinkType"/> instance.</returns>
    public static LinkType ToLinkType(LinkTypeRegistry registry) =>
        new((uint)registry, registry.ToString());

    /// <summary>
    /// Implicitly converts a <see cref="LinkTypeRegistry"/> value to a <see cref="LinkType"/>.
    /// </summary>
    /// <param name="value">The registry value to convert.</param>
    public static implicit operator LinkType(LinkTypeRegistry value) => ToLinkType(value);
}
