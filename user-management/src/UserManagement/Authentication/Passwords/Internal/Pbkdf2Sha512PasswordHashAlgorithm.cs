// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Passwords.Internal;

/// <summary>
/// Built-in PBKDF2-SHA512 password hash algorithm (210,000 iterations, 64-byte digest, 16-byte salt).
/// Registered by default; customers can add additional algorithms via
/// <c>AddPasswordHashAlgorithm&lt;T&gt;()</c>.
/// </summary>
internal sealed class Pbkdf2Sha512PasswordHashAlgorithm : IPasswordHashAlgorithm
{
    private const int SaltLength = 16;
    private const int IterationCount = 210_000;
    private const int DigestSize = 64;
    private const string PrfName = "SHA512";

    private const int MinIterations = 1;
    private const int MaxIterations = 10_000_000;
    private const int MinDigestSize = 1;
    private const int MaxDigestSize = 512;

    private static readonly HashSet<string> AllowedPrfValues =
        [nameof(HashAlgorithmName.SHA1), nameof(HashAlgorithmName.SHA256), nameof(HashAlgorithmName.SHA384), nameof(HashAlgorithmName.SHA512)];

    public string AlgorithmId => Pbkdf2PasswordConstants.AlgorithmId;

    public HashedPasswordData Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            IterationCount,
            HashAlgorithmName.SHA512,
            DigestSize);

        return new HashedPasswordData(
            AlgorithmId,
            hash,
            salt,
            new Dictionary<string, string>
            {
                [Pbkdf2PasswordConstants.ParamPrf] = PrfName,
                [Pbkdf2PasswordConstants.ParamIterations] = IterationCount.ToString(CultureInfo.InvariantCulture),
                [Pbkdf2PasswordConstants.ParamDigestSize] = DigestSize.ToString(CultureInfo.InvariantCulture)
            });
    }

    public bool Verify(string password, HashedPasswordData data)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(data);

        var iterations = data.Parameters.TryGetValue(Pbkdf2PasswordConstants.ParamIterations, out var iterStr) && int.TryParse(iterStr, out var i)
            ? i
            : IterationCount;

        var digestSize = data.Parameters.TryGetValue(Pbkdf2PasswordConstants.ParamDigestSize, out var digestStr) && int.TryParse(digestStr, out var d)
            ? d
            : DigestSize;

        if (iterations is < MinIterations or > MaxIterations || digestSize is < MinDigestSize or > MaxDigestSize)
        {
            return false;
        }

        var prfStr = data.Parameters.GetValueOrDefault(Pbkdf2PasswordConstants.ParamPrf);

        if (prfStr is null || !AllowedPrfValues.Contains(prfStr))
        {
            return false;
        }

        var prf = new HashAlgorithmName(prfStr);

        var derived = Rfc2898DeriveBytes.Pbkdf2(
            password,
            data.Salt.ToArray(),
            iterations,
            prf,
            digestSize);

        return CryptographicOperations.FixedTimeEquals(derived, data.Hash.ToArray());
    }

    public bool NeedsRehash(HashedPasswordData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.AlgorithmId != AlgorithmId)
        {
            return true;
        }

        var prf = data.Parameters.GetValueOrDefault(Pbkdf2PasswordConstants.ParamPrf);
        var iterations = data.Parameters.TryGetValue(Pbkdf2PasswordConstants.ParamIterations, out var iterStr) && int.TryParse(iterStr, out var i)
            ? i
            : 0;
        var digestSize = data.Parameters.TryGetValue(Pbkdf2PasswordConstants.ParamDigestSize, out var digestStr) && int.TryParse(digestStr, out var d)
            ? d
            : 0;

        return prf != PrfName || iterations != IterationCount || digestSize != DigestSize;
    }
}
