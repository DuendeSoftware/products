// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;

namespace Duende.Bff.Yarp;

/// <summary>
/// Default implementation of the message invoker factory.
/// This implementation creates one message invoker per remote API endpoint.
/// </summary>
public class DefaultHttpMessageInvokerFactory : IHttpMessageInvokerFactory
{
    /// <summary>
    /// Dictionary to cache invoker instances
    /// </summary>
    protected readonly ConcurrentDictionary<string, HttpMessageInvoker> Clients = new();

    /// <inheritdoc />
    public virtual HttpMessageInvoker CreateClient(string localPath)
    {
        return Clients.GetOrAdd(localPath, (key) =>
        {
            var handler = CreateHandler(key);
            return new HttpMessageInvoker(handler);
        });
    }
    
    /// <summary>
    /// Creates the HTTP message handler
    /// </summary>
    /// <param name="localPath"></param>
    /// <returns></returns>
    protected virtual HttpMessageHandler CreateHandler(string localPath)
    {
        return new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false
        };
    }
}
