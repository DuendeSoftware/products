// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace MvcJarUriJwt;

public class RequestUriService
{
    private readonly ConcurrentDictionary<string, string> _requestObjects = new();

    public string Set(string value)
    {
        var id = Guid.NewGuid().ToString();
        _requestObjects.TryAdd(id, value);

        return id;
    }

    public string Get(string id)
    {
        if (_requestObjects.TryGetValue(id, out var value))
        {
            return value;
        }

        return null;
    }
}
