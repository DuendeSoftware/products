// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Holds the built-in <see cref="ISignatureVerifier"/> instances registered by UserManagement,
/// in their preference order.
/// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class BuiltInSignatureVerifiers(IReadOnlyList<ISignatureVerifier> verifiers)
{
    /// <summary>
    /// Gets the built-in signature verifiers, in preference order.
    /// </summary>
    public IReadOnlyList<ISignatureVerifier> Verifiers => verifiers;
}

/// <summary>
/// Merges the built-in <see cref="ISignatureVerifier"/> instances with any additional ones
/// registered by the application, without removing and re-adding DI service descriptors.
/// </summary>
/// <remarks>
/// Registered as a singleton, so startup validation and every ceremony share one set of verifiers.
/// Built-in verifiers come first, in their preference order, followed by additional
/// verifiers in DI registration order. Constructing this type never throws, even if the
/// same COSE algorithm is registered more than once, so that
/// <c>UserAuthenticationOptionsValidator</c> can resolve it and report duplicates as a
/// validation failure instead of an exception.
/// </remarks>
internal sealed class SignatureVerifiers
{
    private readonly Lazy<Dictionary<int, ISignatureVerifier>> _byAlgorithm;

    public SignatureVerifiers(BuiltInSignatureVerifiers builtIn, IEnumerable<ISignatureVerifier> additional)
    {
        All = [.. builtIn.Verifiers, .. additional];

        // Built lazily and strictly: a duplicate that bypassed startup validation throws instead of
        // silently picking one verifier, while construction itself stays safe for the options validator.
        _byAlgorithm = new(() => All.ToDictionary(v => v.Algorithm));
    }

    /// <summary>
    /// Gets all signature verifiers: built-in verifiers in their preference order, followed
    /// by additional verifiers in DI registration order.
    /// </summary>
    public IReadOnlyList<ISignatureVerifier> All { get; }

    /// <summary>
    /// Attempts to get the verifier for the given COSE algorithm identifier.
    /// </summary>
    public bool TryGet(int algorithm, [NotNullWhen(true)] out ISignatureVerifier? verifier) =>
        _byAlgorithm.Value.TryGetValue(algorithm, out verifier);

    /// <summary>
    /// Returns whether a verifier is registered for the given COSE algorithm identifier.
    /// </summary>
    public bool Contains(int algorithm) => _byAlgorithm.Value.ContainsKey(algorithm);

    /// <summary>
    /// Creates a <see cref="SignatureVerifiers"/> instance for tests, treating the given
    /// verifiers as the complete set.
    /// </summary>
    internal static SignatureVerifiers Create(params ISignatureVerifier[] verifiers) =>
        new(new BuiltInSignatureVerifiers(verifiers), []);
}
