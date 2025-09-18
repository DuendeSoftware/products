// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Tests.TestInfra;

public static class ObjectExtensions
{

    public static TTarget With<TTarget>(this TTarget target, Action<TTarget> action)
    {
        action(target);
        return target;
    }
}
