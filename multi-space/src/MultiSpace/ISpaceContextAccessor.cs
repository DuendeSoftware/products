// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.MultiSpace;

/// <summary>
/// Provides access to the current space context for the active DI scope.
/// </summary>
/// <remarks>
/// This interface is registered as Scoped. Each DI scope (e.g., HTTP request or job execution)
/// gets its own instance. Call <see cref="SetSpace(SpaceId)"/> once at the start of a new scope (typically
/// by middleware) and then <see cref="GetSpaceId"/> throughout to read it.
/// </remarks>
public interface ISpaceContextAccessor
{
    /// <summary>
    /// Gets the current space ID.
    /// </summary>
    /// <returns>The current space ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no space context has been set.</exception>
    SpaceId GetSpaceId();

    /// <summary>
    /// Determines if the space context has been configured for the current scope.
    /// </summary>
    bool IsSpaceIdConfigured();

    /// <summary>
    /// Returns the current space ID if configured, or <see cref="SpaceId.Default"/> otherwise.
    /// </summary>
    SpaceId GetSpaceIdOrDefault() => IsSpaceIdConfigured() ? GetSpaceId() : SpaceId.Default;

    /// <summary>
    /// Sets the current space ID for this scope.
    /// </summary>
    /// <param name="spaceId">The space ID to set.</param>
    /// <remarks>
    /// This method is idempotent: calling it with the same value more than once is a no-op.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the space context has already been set to a different value for this scope.
    /// </exception>
    void SetSpace(SpaceId spaceId);
}
