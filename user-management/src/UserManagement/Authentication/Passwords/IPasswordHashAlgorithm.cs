// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passwords;

/// <summary>
/// Defines a password hashing algorithm that can hash and verify passwords.
/// Implementations are registered via DI and selected by algorithm ID.
/// </summary>
public interface IPasswordHashAlgorithm
{
    /// <summary>
    /// Gets the unique identifier for this algorithm (e.g., <c>"pbkdf2"</c>).
    /// This value is persisted alongside the hash and used to select the correct
    /// algorithm during verification.
    /// </summary>
    string AlgorithmId { get; }

    /// <summary>
    /// Hashes the given plaintext password, generating a new salt internally.
    /// </summary>
    /// <param name="password">The plaintext password to hash.</param>
    /// <returns>A <see cref="HashedPasswordData"/> containing the hash, salt, and algorithm parameters.</returns>
    HashedPasswordData Hash(string password);

    /// <summary>
    /// Verifies a plaintext password against previously hashed data.
    /// </summary>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="data">The stored hash data produced by a previous call to <see cref="Hash"/>.</param>
    /// <returns><c>true</c> if the password matches; otherwise <c>false</c>.</returns>
    bool Verify(string password, HashedPasswordData data);

    /// <summary>
    /// Determines whether the given hashed password data should be re-hashed
    /// to match this algorithm's current preferred parameters.
    /// Called after successful verification when this algorithm is the preferred one.
    /// </summary>
    /// <param name="data">The stored hash data to evaluate.</param>
    /// <returns><c>true</c> if the password should be re-hashed; otherwise <c>false</c>.</returns>
    bool NeedsRehash(HashedPasswordData data);
}
