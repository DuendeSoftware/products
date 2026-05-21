// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Represents a link operation for batch processing.
/// Creates a link between two entities as part of an atomic batch.
/// </summary>
public sealed class LinkOperation : IStoreOperation
{
    private LinkOperation(LinkDefinition definition, UuidV7 leftEntityId, UuidV7 rightEntityId)
    {
        Definition = definition;
        LeftEntityId = leftEntityId;
        RightEntityId = rightEntityId;
    }

    /// <summary>
    /// Gets the entity type for this operation (the left entity type, for the IStoreOperation contract).
    /// </summary>
    public EntityType EntityType => Definition.Left;

    /// <summary>
    /// Gets the link definition describing the relationship schema.
    /// </summary>
    public LinkDefinition Definition { get; }

    /// <summary>
    /// Gets the ID of the left-side entity.
    /// </summary>
    public UuidV7 LeftEntityId { get; }

    /// <summary>
    /// Gets the ID of the right-side entity.
    /// </summary>
    public UuidV7 RightEntityId { get; }

    /// <summary>
    /// Creates a new link operation.
    /// </summary>
    /// <param name="definition">The link definition.</param>
    /// <param name="leftId">The ID of the left-side entity.</param>
    /// <param name="rightId">The ID of the right-side entity.</param>
    /// <returns>A new link operation.</returns>
    public static LinkOperation For(LinkDefinition definition, UuidV7 leftId, UuidV7 rightId) =>
        new LinkOperation(definition, leftId, rightId);
}
