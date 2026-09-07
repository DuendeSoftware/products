// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Duende.Spaces.Internal;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed implementation of <see cref="ISpaceContextAccessor"/>.
/// </summary>
/// <remarks>
/// The space context flows with the async execution context. Call
/// <see cref="SetSpace(SpaceId)"/> to enter a space scope, and dispose the returned handle
/// to restore the previous context. The middleware wraps downstream execution with a
/// <c>space.request</c> activity; nested calls produce <c>space.override</c> activities.
/// </remarks>
internal sealed class SpaceContextAccessor(ILogger<SpaceContextAccessor> logger) : ISpaceContextAccessor
{
    internal static readonly ActivitySource Source = new(SpacesTracing.ActivitySourceName);

    private readonly AsyncLocal<SpaceId?> _current = new();
    private readonly AsyncLocal<SpaceScope?> _currentScope = new();

    /// <inheritdoc/>
    public SpaceId GetSpaceId()
    {
        if (_current.Value == null)
        {
            throw new InvalidOperationException(
                "No space context has been set. Ensure the space resolution middleware is registered and running before accessing the space context.");
        }

        return _current.Value;
    }

    public bool IsSpaceIdConfigured() => _current.Value is not null;

    /// <inheritdoc/>
    public IDisposable SetSpace(SpaceId spaceId)
    {
        var original = _current.Value;
        var parentScope = _currentScope.Value;
        _current.Value = spaceId;

        var spaceIdString = spaceId.Value.ToString();
        var logScope = logger.BeginScope(new Dictionary<string, object> { ["SpaceId"] = spaceIdString });

        // First call gets a space.request activity (middleware level);
        // nested calls get space.override activities.
        var activityName = original is null ? "space.request" : "space.override";
        var activity = Source.StartActivity(activityName);
        _ = activity?.SetTag("duende.space.id", spaceIdString);

        if (original is not null)
        {
            _ = activity?.SetTag("duende.space.id.original", original.Value.ToString());
        }

        var scope = new SpaceScope(this, parentScope, original, logScope, activity);
        _currentScope.Value = scope;
        return scope;
    }

    private sealed class SpaceScope(
        SpaceContextAccessor accessor,
        SpaceScope? parentScope,
        SpaceId? originalSpaceId,
        IDisposable? logScope,
        Activity? activity) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            if (!ReferenceEquals(accessor._currentScope.Value, this))
            {
                throw new InvalidOperationException("Space scopes must be disposed in reverse order.");
            }

            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            activity?.Dispose();
            logScope?.Dispose();
            accessor._current.Value = originalSpaceId;
            accessor._currentScope.Value = parentScope;
        }
    }

}
