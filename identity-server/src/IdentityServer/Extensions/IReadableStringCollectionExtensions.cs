// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.Extensions.Primitives;

#pragma warning disable 1591

namespace Duende.IdentityServer.Extensions;

public static class IReadableStringCollectionExtensions
{
    [DebuggerStepThrough]
    public static NameValueCollection AsNameValueCollection(this IEnumerable<KeyValuePair<string, StringValues>> collection)
    {
        var nv = new NameValueCollection();

        foreach (var field in collection)
        {
            foreach (var val in field.Value)
            {
                // special check for some Azure product: https://github.com/DuendeSoftware/Support/issues/48
                if (!string.IsNullOrWhiteSpace(val))
                {
                    nv.Add(field.Key, val);
                }
            }
        }

        return nv;
    }

    [DebuggerStepThrough]
    public static NameValueCollection AsNameValueCollection(this IDictionary<string, StringValues> collection)
    {
        var nv = new NameValueCollection();

        foreach (var field in collection)
        {
            foreach (var item in field.Value)
            {
                // special check for some Azure product: https://github.com/DuendeSoftware/Support/issues/48
                if (!string.IsNullOrWhiteSpace(item))
                {
                    nv.Add(field.Key, item);
                }
            }
        }

        return nv;
    }
}
