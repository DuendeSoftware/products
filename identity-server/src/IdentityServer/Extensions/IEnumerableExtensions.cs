// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable 1591

namespace Duende.IdentityServer.Extensions;

internal static class IEnumerableExtensions
{
    extension<T>([NotNullWhen(false)] IEnumerable<T> list)
    {
        [DebuggerStepThrough]
        public bool IsNullOrEmpty()
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
        public bool HasDuplicates<TProp>(Func<T, TProp> selector)
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
}
