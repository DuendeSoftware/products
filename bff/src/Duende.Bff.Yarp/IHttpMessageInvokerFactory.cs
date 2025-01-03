// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net.Http;

namespace Duende.Bff.Yarp;

/// <summary>
/// Factory for creating a HTTP message invoker for outgoing remote BFF API calls
/// </summary>
public interface IHttpMessageInvokerFactory
{
    /// <summary>
    /// Creates a message invoker based on the local path
    /// </summary>
    /// <param name="localPath">Local path the remote API is mapped to</param>
    /// <returns></returns>
    HttpMessageInvoker CreateClient(string localPath);
}