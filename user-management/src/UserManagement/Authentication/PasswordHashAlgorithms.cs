// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passwords;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication;

/// <summary>
/// Provides access to the registered password hash algorithms and the preferred algorithm.
/// This can be used to determine which algorithm should be used when importing password data.
/// </summary>
public sealed class PasswordHashAlgorithms
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordHashAlgorithms"/> class.
    /// </summary>
    /// <param name="algorithms">The registered password hash algorithms.</param>
    /// <param name="options">The user authentication options containing the preferred algorithm configuration.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when duplicate algorithm registrations are detected or when the preferred algorithm is not registered.
    /// </exception>
    public PasswordHashAlgorithms(
        IEnumerable<IPasswordHashAlgorithm> algorithms,
        IOptions<UserAuthenticationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var all = algorithms.ToList();
        var duplicates = all.GroupBy(a => a.AlgorithmId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate IPasswordHashAlgorithm registrations detected for AlgorithmId(s): {string.Join(", ", duplicates.Select(d => $"'{d}'"))}. " +
                $"Each AlgorithmId must be unique.");
        }

        All = all;
        Preferred = all.FirstOrDefault(a => a.AlgorithmId == options.Value.Passwords.PreferredHashAlgorithm)
            ?? throw new InvalidOperationException(
                $"No registered IPasswordHashAlgorithm found with AlgorithmId '{options.Value.Passwords.PreferredHashAlgorithm}'. " +
                $"Ensure the preferred algorithm is registered via AddPasswordHashAlgorithm<T>() or is the built-in default.");
    }

    /// <summary>
    /// Gets the preferred password hash algorithm, as configured in <see cref="PasswordOptions.PreferredHashAlgorithm"/>.
    /// Use this algorithm when hashing passwords for newly imported users.
    /// </summary>
    public IPasswordHashAlgorithm Preferred { get; }

    /// <summary>
    /// Gets all registered password hash algorithms.
    /// </summary>
    public IReadOnlyList<IPasswordHashAlgorithm> All { get; }
}
