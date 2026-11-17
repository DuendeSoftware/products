// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Security.Cryptography;
using Duende.UserManagement.Authentication.Passwords;

namespace PlatformUserManagementApp.Import;

/// <summary>
///     A custom <see cref="IPasswordHashAlgorithm" /> that understands ASP.NET Identity's
///     password hash format (both V2 and V3). This allows imported password hashes to be
///     stored verbatim and verified without conversion.
///     <para>
///         On first successful login, the platform automatically re-hashes the password
///         using the preferred algorithm (PBKDF2-SHA512) because <see cref="NeedsRehash" />
///         always returns <c>true</c>.
///     </para>
/// </summary>
public sealed class AspNetIdentityPasswordHashAlgorithm : IPasswordHashAlgorithm
{
    /// <summary>
    ///     The algorithm identifier used to tag imported ASP.NET Identity password hashes.
    /// </summary>
    public const string Id = "aspnet-identity";

    /// <summary>
    ///     Parameter key for the base64-encoded ASP.NET Identity password hash blob.
    /// </summary>
    internal const string ParamBlob = "blob";

    /// <inheritdoc />
    public string AlgorithmId => Id;

    /// <inheritdoc />
    /// <remarks>
    ///     Hashing new passwords with this algorithm is not supported — it exists only to verify
    ///     imported hashes. The platform's preferred algorithm handles new password hashing.
    /// </remarks>
    public HashedPasswordData Hash(string password) =>
        throw new NotSupportedException(
            "AspNetIdentityPasswordHashAlgorithm is for verifying imported hashes only. " +
            "New passwords should be hashed with the platform's preferred algorithm.");

    /// <inheritdoc />
    public bool Verify(string password, HashedPasswordData data)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(data);

        if (!data.Parameters.TryGetValue(ParamBlob, out var blob))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(blob);
        }
        catch (FormatException)
        {
            return false;
        }

        if (bytes.Length == 0)
        {
            return false;
        }

        return bytes[0] switch
        {
            0x00 => VerifyV2(password, bytes),
            0x01 => VerifyV3(password, bytes),
            _ => false
        };
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Always returns <c>true</c> so that on first successful login the password is
    ///     re-hashed with the platform's preferred algorithm (PBKDF2-SHA512 by default).
    /// </remarks>
    public bool NeedsRehash(HashedPasswordData data) => true;

    /// <summary>
    ///     Creates a <see cref="HashedPasswordData" /> that wraps an ASP.NET Identity password hash
    ///     for import into the platform. The original base64 blob is stored as-is.
    /// </summary>
    public static HashedPasswordData CreateImportData(string aspNetPasswordHash) =>
        new(
            Id,
            [],
            [],
            new Dictionary<string, string> { [ParamBlob] = aspNetPasswordHash });

    /// <summary>
    ///     ASP.NET Identity V2 format: 0x00 || salt (16 bytes) || hash (32 bytes).
    ///     PBKDF2-SHA1 with 1,000 iterations.
    /// </summary>
    private static bool VerifyV2(string password, byte[] bytes)
    {
        const int saltSize = 16;
        const int hashSize = 32;
        const int expectedLength = 1 + saltSize + hashSize;

        if (bytes.Length != expectedLength)
        {
            return false;
        }

        var salt = bytes.AsSpan(1, saltSize);
        var storedHash = bytes.AsSpan(1 + saltSize, hashSize);

        var derived = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            1000,
            HashAlgorithmName.SHA1,
            hashSize);

        return CryptographicOperations.FixedTimeEquals(derived, storedHash);
    }

    /// <summary>
    ///     ASP.NET Identity V3 format: 0x01 || PRF (4 bytes BE) || iterations (4 bytes BE) ||
    ///     salt length (4 bytes BE) || salt || hash.
    /// </summary>
    private static bool VerifyV3(string password, byte[] bytes)
    {
        const int headerSize = 13;

        if (bytes.Length < headerSize)
        {
            return false;
        }

        var prf = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1, 4));
        var rawIterations = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4));
        var rawSaltLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(9, 4));

        if (rawIterations is 0 or > int.MaxValue || rawSaltLength is 0 or > 128)
        {
            return false;
        }

        var iterations = (int)rawIterations;
        var saltLength = (int)rawSaltLength;

        var expectedMinLength = headerSize + saltLength;
        if (bytes.Length <= expectedMinLength)
        {
            return false;
        }

        var salt = bytes.AsSpan(headerSize, saltLength);
        var storedHash = bytes.AsSpan(headerSize + saltLength);

        var hashAlgorithm = prf switch
        {
            0 => HashAlgorithmName.SHA1,
            1 => HashAlgorithmName.SHA256,
            2 => HashAlgorithmName.SHA512,
            _ => default
        };

        if (hashAlgorithm == default)
        {
            return false;
        }

        var derived = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            hashAlgorithm,
            storedHash.Length);

        return CryptographicOperations.FixedTimeEquals(derived, storedHash);
    }
}
