// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Strongly-typed identifier for a passkey credential ID from a WebAuthn authenticator.
/// </summary>
public readonly struct PasskeyCredentialId : IEquatable<PasskeyCredentialId>
{
    // https://www.w3.org/TR/webauthn-3/#credential-id
    private const int MaxLength = 1023;

    /// <summary>
    /// Disallow default construction.
    /// </summary>
    public PasskeyCredentialId() => throw new InvalidOperationException();

    private PasskeyCredentialId(byte[] bytes) => Bytes = bytes;

    private byte[] Bytes { get; }

    /// <summary>
    /// Creates a PasskeyCredentialId from a byte array with validation.
    /// </summary>
    /// <param name="value">The credential ID bytes.</param>
    /// <returns>A valid PasskeyCredentialId.</returns>
    /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
    /// <exception cref="ArgumentException">Thrown when value is empty or exceeds maximum length.</exception>
    public static PasskeyCredentialId From(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length switch
        {
            0 => throw new ArgumentException("Credential ID cannot be empty.", nameof(value)),
            > MaxLength => throw new ArgumentException(
                $"Credential ID length {value.Length} exceeds maximum of {MaxLength} bytes.",
                nameof(value)),
            _ => new PasskeyCredentialId(value.ToArray()) // Defensive copy
        };
    }

    /// <summary>
    /// Attempts to create a PasskeyCredentialId from a byte array.
    /// </summary>
    /// <param name="value">The credential ID bytes.</param>
    /// <param name="result">The created PasskeyCredentialId if successful.</param>
    /// <returns>True if the credential ID was created successfully; otherwise, false.</returns>
    public static bool TryFrom(byte[]? value, [NotNullWhen(true)] out PasskeyCredentialId? result)
    {
        result = null;

        if (value is null || value.Length == 0 || value.Length > MaxLength)
        {
            return false;
        }

        result = new PasskeyCredentialId(value.ToArray());
        return true;
    }

    /// <summary>
    /// Loads a PasskeyCredentialId from trusted storage without validation.
    /// </summary>
    internal static PasskeyCredentialId Load(byte[] bytes) => new(bytes);

    /// <summary>
    /// Returns the credential ID as a byte array.
    /// </summary>
    public byte[] ToBytes() => Bytes.ToArray();

    /// <summary>
    /// Returns the credential ID as a Base64 string.
    /// </summary>
    public string ToBase64String() => Convert.ToBase64String(ToBytes());

    public override string ToString() => ToBase64String();

    public override bool Equals(object? obj) => obj is PasskeyCredentialId other && Equals(other);

    public bool Equals(PasskeyCredentialId other) =>
        CryptographicOperations.FixedTimeEquals(Bytes, other.Bytes);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var b in Bytes)
        {
            hash.Add(b);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(PasskeyCredentialId left, PasskeyCredentialId right) =>
        left.Equals(right);

    public static bool operator !=(PasskeyCredentialId left, PasskeyCredentialId right) =>
        !left.Equals(right);
}
