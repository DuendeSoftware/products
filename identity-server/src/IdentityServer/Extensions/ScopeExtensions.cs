// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Duende.IdentityServer.Models;

internal static class ScopeExtensions
{
    extension(IEnumerable<ApiScope> apiScopes)
    {
        [DebuggerStepThrough]
        public string ToSpaceSeparatedString()
        {
            var scopeNames = from s in apiScopes
                             select s.Name;

            return string.Join(' ', scopeNames);
        }

        [DebuggerStepThrough]
        public IEnumerable<string> ToStringList()
        {
            var scopeNames = from s in apiScopes
                             select s.Name;

            return scopeNames;
        }
    }
}
