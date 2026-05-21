// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Builds a <see cref="LinkQueryDescriptor"/> using a fluent API.
/// </summary>
public sealed class LinkQueryBuilder
{
    private readonly EntityType _source;
    private readonly List<LinkQueryJoin> _joins = [];
    private EntityType? _currentEndpoint;
    private EntityType? _whereEntityType;
    private UuidV7? _whereEntityId;
    private bool _whereSet;

    internal LinkQueryBuilder(EntityType source)
    {
        _source = source;
        _currentEndpoint = source;
    }

    /// <summary>
    /// Adds a link traversal hop. The definition must connect to the current
    /// chain endpoint — either <c>definition.Left</c> or <c>definition.Right</c>
    /// must match. Direction is determined automatically.
    /// </summary>
    public LinkQueryBuilder Join(LinkDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        LinkJoinDirection direction;
        if (definition.Left == _currentEndpoint)
        {
            direction = LinkJoinDirection.LeftToRight;
            _currentEndpoint = definition.Right;
        }
        else if (definition.Right == _currentEndpoint)
        {
            direction = LinkJoinDirection.RightToLeft;
            _currentEndpoint = definition.Left;
        }
        else
        {
            throw new InvalidOperationException(
                $"LinkDefinition '{definition.Link.Name}' does not connect to the current chain endpoint '{_currentEndpoint?.Name}'. " +
                $"Expected Left='{definition.Left.Name}' or Right='{definition.Right.Name}' to match.");
        }

        _joins.Add(new LinkQueryJoin(definition, direction));
        return this;
    }

    /// <summary>
    /// Adds a filter: only return source entities reachable from this specific entity.
    /// Can only be called once. The entity type must be the terminal type of the chain
    /// (the endpoint after all joins).
    /// </summary>
    public LinkQueryBuilder Where(EntityType entityType, UuidV7 entityId)
    {
        if (_whereSet)
        {
            throw new InvalidOperationException("Where has already been set. Only one Where filter is allowed per query.");
        }

        if (entityType != _currentEndpoint)
        {
            throw new InvalidOperationException(
                $"Entity type '{entityType.Name}' is not the terminal type of the link query chain. " +
                $"Where can only filter on the terminal type '{_currentEndpoint?.Name}'.");
        }

        _whereEntityType = entityType;
        _whereEntityId = entityId;
        _whereSet = true;
        return this;
    }

    /// <summary>
    /// Builds the <see cref="LinkQueryDescriptor"/>. Requires at least one Join.
    /// </summary>
    public LinkQueryDescriptor Build()
    {
        if (_joins.Count == 0)
        {
            throw new InvalidOperationException("At least one Join is required to build a link query.");
        }

        return new LinkQueryDescriptor(_source, _joins.AsReadOnly(), _whereEntityType, _whereEntityId);
    }

}
