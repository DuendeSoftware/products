// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Spaces;

/// <summary>
/// Provides access to the current space context for the active async execution flow.
/// </summary>
/// <remarks>
/// This interface is backed by <see cref="AsyncLocal{T}"/> and registered as Singleton.
/// The space context flows with the async execution context (e.g., HTTP request or job execution).
/// Call <see cref="SetSpace(SpaceId)"/> at the start of an async flow (typically by middleware)
/// and then <see cref="GetSpaceId"/> throughout to read it. Use <c>using</c> to scope the
/// space context -- when the returned <see cref="IDisposable"/> is disposed, the previous
/// space context is restored.
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
    /// Determines if the space context has been configured for the current async flow.
    /// </summary>
    bool IsSpaceIdConfigured();

    /// <summary>
    /// Returns the current space ID if configured, or <see cref="SpaceId.Default"/> otherwise.
    /// </summary>
    SpaceId GetSpaceIdOrDefault() => IsSpaceIdConfigured() ? GetSpaceId() : SpaceId.Default;

    /// <summary>
    /// Sets the current space ID for this async flow and returns a scope that restores
    /// the previous space context when disposed.
    /// </summary>
    /// <param name="spaceId">The space ID to set.</param>
    /// <returns>An <see cref="IDisposable"/> that restores the previous space context when disposed.</returns>
    /// <remarks>
    /// Always use with <c>using</c> to ensure the space context is properly restored:
    /// <code>
    /// using (accessor.SetSpace(spaceId))
    /// {
    ///     // work in spaceId context
    /// }
    /// // previous context restored
    /// </code>
    /// </remarks>
    IDisposable SetSpace(SpaceId spaceId);
}
