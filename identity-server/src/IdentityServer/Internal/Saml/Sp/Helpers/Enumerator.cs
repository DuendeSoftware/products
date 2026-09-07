// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections;

namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    static class Enumerator
    {
        class GenericEnumeratorAdapter<T> : IEnumerator<T>
        {
            private readonly IEnumerator source;

            public GenericEnumeratorAdapter(IEnumerator source)
            {
                this.source = source;
            }

            public T Current
            {
                get { return (T)source.Current; }
            }

            public void Dispose()
            {
                // Non generic IEnumerator doesn't implement IDisposable so do nothing.
            }

            object System.Collections.IEnumerator.Current
            {
                get
                {
                    return source.Current;
                }
            }

            public bool MoveNext()
            {
                return source.MoveNext();
            }

            public void Reset()
            {
                source.Reset();
            }
        }

        extension(IEnumerator source)
        {
            public IEnumerator<T> AsGeneric<T>()
            {
                return new GenericEnumeratorAdapter<T>(source);
            }
        }
    }
}
