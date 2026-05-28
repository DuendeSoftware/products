// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class LocalizedEntryCollection<T> : IEnumerable<T>
        where T : LocalizedEntry
    {
        private List<T> items = new List<T>();

        public void Add(T item) => items.Add(item);

        public void Clear() => items.Clear();

        public int Count => items.Count;

        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();
    }
}
