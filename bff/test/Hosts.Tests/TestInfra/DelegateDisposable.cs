// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Hosts.Tests.TestInfra;

public class DelegateDisposable(Action onDispose) : IDisposable
{
    public void Dispose()
    {
        onDispose();
    }
}
