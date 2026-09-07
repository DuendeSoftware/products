// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Spaces;

public class SpaceContextAccessorTelemetryTests : IDisposable
{
    private readonly ISpaceContextAccessor _sut;
    private readonly CapturingLoggerProvider _logProvider;
    private readonly ConcurrentBag<Activity> _stoppedActivities = new();
    private readonly ActivityListener _listener;

    public SpaceContextAccessorTelemetryTests()
    {
        _logProvider = new CapturingLoggerProvider();

        var sc = new ServiceCollection();
        sc.AddLogging(b => b.AddProvider(_logProvider));
        sc.AddSpaces();
        var sp = sc.BuildServiceProvider();
        _sut = sp.GetRequiredService<ISpaceContextAccessor>();

        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SpacesTracing.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _stoppedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void set_space_starts_activity_with_space_id_tag()
    {
        var spaceId = SpaceId.New();
        using var scope = _sut.SetSpace(spaceId);

        var current = Activity.Current;
        current.ShouldNotBeNull();
        current.OperationName.ShouldBe("space.request");
        current.GetTagItem("duende.space.id").ShouldBe(spaceId.Value.ToString());
    }

    [Fact]
    public void set_space_adds_space_id_to_log_scope()
    {
        var spaceId = SpaceId.New();
        using var scope = _sut.SetSpace(spaceId);

        _logProvider.Scopes.ShouldContain(s =>
            s.ContainsKey("SpaceId") && s["SpaceId"].ToString() == spaceId.Value.ToString());
    }

    [Fact]
    public void nested_set_space_starts_override_activity()
    {
        var original = SpaceId.New();
        var overridden = SpaceId.New();
        using var outer = _sut.SetSpace(original);

        using (_sut.SetSpace(overridden))
        {
            var current = Activity.Current;
            current.ShouldNotBeNull();
            current.OperationName.ShouldBe("space.override");
            current.GetTagItem("duende.space.id").ShouldBe(overridden.Value.ToString());
            current.GetTagItem("duende.space.id.original").ShouldBe(original.Value.ToString());
        }
    }

    [Fact]
    public void dispose_stops_activity()
    {
        var spaceId = SpaceId.New();
        var scope = _sut.SetSpace(spaceId);
        scope.Dispose();

        _stoppedActivities.ShouldContain(a =>
            a.GetTagItem("duende.space.id")!.ToString() == spaceId.Value.ToString());
    }

    [Fact]
    public void dispose_restores_parent_activity()
    {
        var original = SpaceId.New();
        using var outer = _sut.SetSpace(original);
        var parentActivity = Activity.Current;

        using (_sut.SetSpace(SpaceId.New()))
        {
            Activity.Current.ShouldNotBe(parentActivity);
        }

        Activity.Current.ShouldBe(parentActivity);
    }

    [Fact]
    public void nested_scopes_produce_nested_activities()
    {
        var level0 = SpaceId.New();
        var level1 = SpaceId.New();
        var level2 = SpaceId.New();

        using var l0Scope = _sut.SetSpace(level0);
        var l0Activity = Activity.Current;

        using (_sut.SetSpace(level1))
        {
            var l1Activity = Activity.Current;
            l1Activity.ShouldNotBeNull();
            l1Activity.GetTagItem("duende.space.id").ShouldBe(level1.Value.ToString());

            using (_sut.SetSpace(level2))
            {
                var l2Activity = Activity.Current;
                l2Activity.ShouldNotBeNull();
                l2Activity.GetTagItem("duende.space.id").ShouldBe(level2.Value.ToString());
                l2Activity.Parent.ShouldBe(l1Activity);
            }

            Activity.Current.ShouldBe(l1Activity);
        }

        Activity.Current.ShouldBe(l0Activity);
    }

    [Fact]
    public void dispose_removes_log_scope()
    {
        using var outer = _sut.SetSpace(SpaceId.New());

        var scopeCountBefore = _logProvider.Scopes.Count;

        var inner = _sut.SetSpace(SpaceId.New());
        _logProvider.Scopes.Count.ShouldBe(scopeCountBefore + 1);

        inner.Dispose();
        _logProvider.DisposedScopeCount.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// A logger provider that captures scopes for assertion.
    /// </summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<Dictionary<string, object>> Scopes { get; } = new();
        private int _disposedScopeCount;
        public int DisposedScopeCount => _disposedScopeCount;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                if (state is IEnumerable<KeyValuePair<string, object>> kvps)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var kvp in kvps)
                    {
                        dict[kvp.Key] = kvp.Value;
                    }
                    provider.Scopes.Enqueue(dict);
                }

                return new ScopeDisposable(provider);
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }

        private sealed class ScopeDisposable(CapturingLoggerProvider provider) : IDisposable
        {
            public void Dispose() => Interlocked.Increment(ref provider._disposedScopeCount);
        }
    }
}
