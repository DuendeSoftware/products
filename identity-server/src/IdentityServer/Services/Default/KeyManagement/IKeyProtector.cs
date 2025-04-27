// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Interface to model protecting/unprotecting RsaKeyContainer.
/// </summary>
public interface ISigningKeyProtector
{
    /// <summary>
    /// Protects KeyContainer.
    /// </summary>
    /// <param name="key"></param>
    SerializedKey Protect(KeyContainer key);

    /// <summary>
    /// Unprotects KeyContainer.
    /// </summary>
    /// <param name="key"></param>
    KeyContainer Unprotect(SerializedKey key);
}
