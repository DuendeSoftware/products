// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// The type of link between two entities.
/// </summary>
/// <param name="Id">A number representation for the link type.</param>
/// <param name="Name">The name of the link type. This name is only used for display purposes and should never change.</param>
public readonly record struct LinkType(uint Id, string Name)
{
    [Obsolete("Don't use this constructor")]
    public LinkType() : this(0!, null!) => throw new InvalidOperationException("Cannot instantiate LinkType without parameters");

    public static LinkType ToLinkType(LinkTypeRegistry registry) =>
        new((uint)registry, registry.ToString());

    public static implicit operator LinkType(LinkTypeRegistry value) => ToLinkType(value);
}
