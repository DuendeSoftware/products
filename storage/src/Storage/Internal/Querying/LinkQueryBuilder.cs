// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Builds a <see cref="LinkQueryDescriptor"/> using a fluent API.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed class LinkQueryBuilder
{
    /// <summary>The maximum number of entity ids accepted by <see cref="WhereAny"/>.</summary>
    public const int MaxWhereAnyEntityIds = 1000;

    private readonly EntityType _source;
    private readonly List<LinkQueryJoin> _joins = [];
    private EntityType? _currentEndpoint;
    private EntityType? _whereEntityType;
    private UuidV7? _whereEntityId;
    private IReadOnlySet<UuidV7>? _whereEntityIds;
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
        EnsureWhereCanBeSet(entityType, nameof(Where));

        _whereEntityType = entityType;
        _whereEntityId = entityId;
        _whereSet = true;
        return this;
    }

    /// <summary>
    /// Adds a filter: only return source entities reachable from any of these entities, keeping
    /// track of which entity each result was reached from. Use it with
    /// <c>IPartitionedStorage.QueryLinksByAnchorAsync</c> and <c>IPartitionedStorage.CountLinksAsync</c>.
    /// Mutually exclusive with <see cref="Where"/>, and can only be called once. The entity type must be
    /// the terminal type of the chain (the endpoint after all joins). An empty set is allowed.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="entityIds"/> has more than <see cref="MaxWhereAnyEntityIds"/> entries
    /// (thrown when the descriptor is built).
    /// </exception>
    public LinkQueryBuilder WhereAny(EntityType entityType, IReadOnlySet<UuidV7> entityIds)
    {
        ArgumentNullException.ThrowIfNull(entityIds);

        EnsureWhereCanBeSet(entityType, nameof(WhereAny));

        _whereEntityType = entityType;
        _whereEntityIds = entityIds;
        _whereSet = true;
        return this;
    }

    private void EnsureWhereCanBeSet(EntityType entityType, string method)
    {
        if (_whereSet)
        {
            throw new InvalidOperationException(
                "A Where or WhereAny filter has already been set. Only one of them is allowed per query, once.");
        }

        if (entityType != _currentEndpoint)
        {
            throw new InvalidOperationException(
                $"Entity type '{entityType.Name}' is not the terminal type of the link query chain. " +
                $"{method} can only filter on the terminal type '{_currentEndpoint?.Name}'.");
        }
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

        if (_whereSet && _whereEntityType != _currentEndpoint)
        {
            throw new InvalidOperationException(
                $"The Where or WhereAny filter is on '{_whereEntityType?.Name}', but the chain now ends at '{_currentEndpoint?.Name}'. " +
                "Add every Join before the filter.");
        }

        // Copy the joins so a later Join on this builder can't change a descriptor that's already built.
        return new LinkQueryDescriptor(_source, _joins.ToArray(), _whereEntityType, _whereEntityId)
        {
            WhereEntityIds = _whereEntityIds
        };
    }

}
