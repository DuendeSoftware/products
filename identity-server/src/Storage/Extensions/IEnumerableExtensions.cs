// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics;

#pragma warning disable 1591

namespace Duende.IdentityServer.Extensions;

internal static class IEnumerableExtensions
{
    extension<T>(IEnumerable<T> list)
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
    }
}
