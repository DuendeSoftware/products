// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;

namespace Duende.Storage.Internal;

/// <summary>
/// Generates deterministic GUIDs from string inputs using MD5 hashing.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public static class DeterministicGuidGenerator
{
    /// <summary>
    /// Creates a deterministic GUID from the specified name.
    /// </summary>
    /// <param name="name">The name to generate a GUID from.</param>
    /// <returns>A deterministic GUID derived from the input name.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
    public static Guid Create(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(name));
        }
        //use MD5 hash to get a 16-byte hash of the string: 

        var bytes = Encoding.Default.GetBytes(name);

#pragma warning disable CA5351 // MD5 is used to produce a deterministic 128-bit hash for stable GUID generation from names, not for cryptographic security
        var hashBytes = MD5.HashData(bytes);
#pragma warning restore CA5351

        //generate a guid from the hash: 

        var hashGuid = new Guid(hashBytes);

        return hashGuid;
    }
}
