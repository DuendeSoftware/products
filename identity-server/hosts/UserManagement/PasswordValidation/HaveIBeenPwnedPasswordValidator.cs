// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Passwords;

namespace PasswordValidation;

/// <summary>
/// A sample <see cref="IPasswordValidator"/> that checks passwords against
/// the HaveIBeenPwned Passwords API v3 using k-anonymity (hash prefix query).
///
/// This implementation sends only the first 5 characters of the SHA-1 hash to
/// the HIBP API, so the full password is never transmitted over the network.
/// See https://haveibeenpwned.com/API/v3#PwnedPasswords for API documentation.
/// </summary>
public sealed class HaveIBeenPwnedPasswordValidator : IPasswordValidator
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// The minimum number of times a password must have appeared in breaches
    /// before it is rejected. Defaults to 1 (reject if seen at all).
    /// </summary>
    public int MinimumBreachCount { get; init; } = 1;

    /// <summary>
    /// Initializes a new instance of <see cref="HaveIBeenPwnedPasswordValidator"/>.
    /// </summary>
    public HaveIBeenPwnedPasswordValidator(IHttpClientFactory httpClientFactory) =>
        _httpClientFactory = httpClientFactory;

    /// <inheritdoc />
    public async Task<PasswordValidationResult> ValidateAsync(UserSubjectId userId, string password, Ct ct)
    {
        // Compute SHA-1 hash of the password
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        // The HIBP Passwords API v3 requires SHA-1 hashes — this usage is intentional.
#pragma warning disable CA5350 // Do not use weak cryptographic algorithms
        var hashBytes = SHA1.HashData(passwordBytes);
#pragma warning restore CA5350
        var hash = Convert.ToHexString(hashBytes); // uppercase hex, e.g. "5BAA61..."

        // Use k-anonymity: only send the first 5 characters of the hash
        var prefix = hash[..5];
        var suffix = hash[5..];

        var httpClient = _httpClientFactory.CreateClient("hibp");
        try
        {
            using var response = await httpClient.GetAsync(
                new Uri($"https://api.pwnedpasswords.com/range/{prefix}"),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return new PasswordValidationResult.Accepted();
            }

            var body = await response.Content.ReadAsStringAsync(ct);

            // Each line in the response is "<SUFFIX>:<count>"
            foreach (var line in body.AsSpan().EnumerateLines())
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0)
                {
                    continue;
                }

                var responseSuffix = line[..colonIndex];
                if (!responseSuffix.Equals(suffix.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (int.TryParse(line[(colonIndex + 1)..], out var count) && count >= MinimumBreachCount)
                {
                    return new PasswordValidationResult.Rejected(
                        "Password has appeared in a known data breach");
                }
            }

            return new PasswordValidationResult.Accepted();
        }
        catch (HttpRequestException)
        {
            // If the HIBP API is unavailable, fail open (allow the password)
            // so a transient outage does not block all password operations.
            // Change this to fail closed if your threat model requires it.
            return new PasswordValidationResult.Accepted();
        }
    }
}
