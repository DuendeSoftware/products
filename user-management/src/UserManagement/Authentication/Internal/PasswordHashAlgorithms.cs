// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passwords;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Internal;

internal sealed class PasswordHashAlgorithms
{
    public PasswordHashAlgorithms(
        IEnumerable<IPasswordHashAlgorithm> algorithms,
        IOptions<UserAuthenticationOptions> options)
    {
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

    public IPasswordHashAlgorithm Preferred { get; }

    public IReadOnlyList<IPasswordHashAlgorithm> All { get; }
}
