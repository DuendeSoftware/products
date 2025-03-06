// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable 1591

namespace Duende.IdentityServer.Extensions;

public static class IEnumerableExtensions
{
    [DebuggerStepThrough]
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T> list)
    {
        if (list == null)
        {
            return true;
        }

        if (!list.Any())
        {
            return true;
        }

        return false;
    }

    public static bool HasDuplicates<T, TProp>(this IEnumerable<T> list, Func<T, TProp> selector)
    {
        var d = new HashSet<TProp>();
        foreach (var t in list)
        {
            if (!d.Add(selector(t)))
            {
                return true;
            }
        }
        return false;
    }
}