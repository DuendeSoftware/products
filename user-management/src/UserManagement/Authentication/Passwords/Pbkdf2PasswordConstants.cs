// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passwords;

/// <summary>
/// Well-known constants for the built-in PBKDF2 password hash algorithm.
/// Use these when constructing <see cref="HashedPasswordData"/> for imported passwords.
/// </summary>
public static class Pbkdf2PasswordConstants
{
    /// <summary>
    /// The algorithm identifier for the built-in PBKDF2-SHA512 algorithm.
    /// </summary>
    public const string AlgorithmId = "pbkdf2";

    /// <summary>
    /// The parameter key for the pseudorandom function name (e.g. "SHA512").
    /// </summary>
    public const string ParamPrf = "prf";

    /// <summary>
    /// The parameter key for the iteration count.
    /// </summary>
    public const string ParamIterations = "iterations";

    /// <summary>
    /// The parameter key for the hash digest size in bytes.
    /// </summary>
    public const string ParamDigestSize = "digestSize";
}
