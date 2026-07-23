// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.MultiSpace.Internal;

/// <summary>
/// Default implementation of <see cref="ISpaceContextAccessor"/> using a simple field.
/// </summary>
/// <remarks>
/// Registered as Scoped — each DI scope (HTTP request, job execution, etc.) gets its own
/// instance. The space is set once via <see cref="SetSpace(SpaceId)"/> on a fresh scope and read via
/// <see cref="GetSpaceId"/> throughout the scope's lifetime.
/// </remarks>
internal sealed class SpaceContextAccessor : ISpaceContextAccessor
{
    private SpaceId? _current;

    /// <inheritdoc/>
    public SpaceId GetSpaceId()
    {
        if (_current == null)
        {
            throw new InvalidOperationException(
                "No space context has been set. Ensure the space resolution middleware is registered and running before accessing the space context.");
        }

        return _current!;
    }

    public bool IsSpaceIdConfigured() => _current is not null;

    /// <inheritdoc/>
    public void SetSpace(SpaceId spaceId)
    {
        if (_current is not null)
        {
            if (_current == spaceId)
            {
                return;
            }

            throw new InvalidOperationException(
                "The space context has already been set to a different value for this scope. " +
                "Create a new DI scope before setting a different space context.");
        }

        _current = spaceId;
    }
}
