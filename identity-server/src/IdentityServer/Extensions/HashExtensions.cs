// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography;
using System.Text;
using Duende.IdentityServer.Extensions;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Extension methods for hashing strings
/// </summary>
public static class HashExtensions
{
    /// <summary>
    /// Creates a SHA256 hash of the specified input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>A hash</returns>
    public static string Sha256(this string input)
    {
        if (input.IsMissing())
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);

        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Creates a SHA256 hash of the specified input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>A hash.</returns>
    public static byte[] Sha256(this byte[] input)
    {
        if (input == null)
        {
            return null;
        }

        return SHA256.HashData(input);
    }

    /// <summary>
    /// Creates a SHA512 hash of the specified input.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>A hash</returns>
    public static string Sha512(this string input)
    {
        if (input.IsMissing())
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA512.HashData(bytes);

        return Convert.ToBase64String(hash);
    }
}
