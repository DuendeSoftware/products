// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.Extensions.Primitives;

#pragma warning disable 1591

namespace Duende.IdentityServer.Extensions;

public static class IReadableStringCollectionExtensions
{
    extension(IEnumerable<KeyValuePair<string, StringValues>> collection)
    {
        [DebuggerStepThrough]
        public NameValueCollection AsNameValueCollection()
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
    }

    extension(IDictionary<string, StringValues> collection)
    {
        [DebuggerStepThrough]
        public NameValueCollection AsNameValueCollection()
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
}
