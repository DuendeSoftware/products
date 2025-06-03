// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.Bff.Tests.TestInfra;

public static class TestOptionsMonitor
{
    public static TestOptionsMonitor<T> Create<T>(T value) => new(value);
}

public class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    private readonly List<Action<T, string?>> _actions = new();
    private T _currentValue = value;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener)
    {
        _actions.Add(listener);
        return new DelegateDisposable(() =>
        {
            _actions.Remove(listener);
        });
    }

    public T CurrentValue
    {
        get => _currentValue;
        set
        {
            _currentValue = value;
            foreach (var action in _actions)
            {
                action(_currentValue, string.Empty);
            }
        }
    }

    public class DelegateDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

}
