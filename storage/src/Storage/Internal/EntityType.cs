// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// The type of document that's being stored.
/// Each library defines its own entity types as static fields on the containing DSO class.
/// </summary>
/// <param name="Id">A number representation for the DSO type. Must be unique across all entity types.</param>
/// <param name="Name">The name of the DSO type. Used for analytics and display purposes. Should never change once entities exist.</param>
public readonly record struct EntityType(uint Id, string Name)
{
    [Obsolete("Don't use this constructor")]
    public EntityType() : this(0, null!) => throw new InvalidOperationException("Cannot instantiate EntityType without parameters");
}
