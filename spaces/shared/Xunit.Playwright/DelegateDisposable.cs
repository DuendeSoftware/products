// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Xunit.Playwright;

public class DelegateDisposable(Action onDispose) : IDisposable
{
    public void Dispose() => onDispose();
}
