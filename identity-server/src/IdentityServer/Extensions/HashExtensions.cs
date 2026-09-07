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
    extension(string input)
    {
        /// <summary>
        /// Creates a SHA256 hash of the specified input.
        /// </summary>
        /// <returns>A hash</returns>
        public string Sha256()
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
        /// Creates a SHA512 hash of the specified input.
        /// </summary>
        /// <returns>A hash</returns>
        public string Sha512()
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

    extension(byte[] input)
    {
        /// <summary>
        /// Creates a SHA256 hash of the specified input.
        /// </summary>
        /// <returns>A hash.</returns>
        public byte[] Sha256()
        {
            if (input == null)
            {
                return null;
            }

            return SHA256.HashData(input);
        }
    }
}
