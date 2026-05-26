// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passwords;

/// <summary>
/// Contains the data produced by a password hashing operation.
/// This is the public data contract that <see cref="IPasswordHashAlgorithm"/> implementations
/// produce when hashing and consume when verifying.
/// </summary>
public sealed class HashedPasswordData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HashedPasswordData"/> class.
    /// </summary>
    /// <param name="algorithmId">The identifier of the algorithm that produced this hash.</param>
    /// <param name="hash">The derived hash bytes.</param>
    /// <param name="salt">The salt bytes used during hashing.</param>
    /// <param name="parameters">Algorithm-specific parameters (e.g., iteration count, memory size).</param>
    public HashedPasswordData(
        string algorithmId,
        IReadOnlyList<byte> hash,
        IReadOnlyList<byte> salt,
        IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(algorithmId);
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(parameters);

        AlgorithmId = algorithmId;
        Hash = hash;
        Salt = salt;
        Parameters = parameters;
    }

    /// <summary>
    /// The identifier of the algorithm that produced this hash.
    /// </summary>
    public string AlgorithmId { get; }

    /// <summary>
    /// The derived hash bytes.
    /// </summary>
    public IReadOnlyList<byte> Hash { get; }

    /// <summary>
    /// The salt bytes used during hashing.
    /// </summary>
    public IReadOnlyList<byte> Salt { get; }

    /// <summary>
    /// Algorithm-specific parameters (e.g., iteration count, memory size).
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <inheritdoc />
    public override string ToString() => $"HashedPasswordData {{ AlgorithmId = {AlgorithmId} }}";
}
