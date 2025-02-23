// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Contract for a service that can serialize and deserialize pushed
/// authorization requests.
/// </summary>
public interface IPushedAuthorizationSerializer
{
    /// <summary>
    /// Unprotects and deserializes the pushed authorization parameters
    /// </summary>
    /// <param name="parameters">The data protected, serialized raw parameters.</param>
    /// <returns>The unprotected parameters, parsed into a NameValueCollection.</returns>
    NameValueCollection Deserialize(string parameters);

    /// <summary>
    /// Protects and serializes pushed authorization parameters.
    /// </summary>
    /// <param name="raw">The raw parameter collection.</param>
    /// <returns>The parameters, serialized and data protected.</returns>
    string Serialize(NameValueCollection raw);
}
