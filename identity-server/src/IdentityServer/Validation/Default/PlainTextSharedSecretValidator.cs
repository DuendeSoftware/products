// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validates a secret stored in plain text
/// </summary>
public class PlainTextSharedSecretValidator : ISecretValidator
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlainTextSharedSecretValidator"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PlainTextSharedSecretValidator(ILogger<PlainTextSharedSecretValidator> logger) => _logger = logger;

    /// <summary>
    /// Validates a secret
    /// </summary>
    /// <param name="secrets">The stored secrets.</param>
    /// <param name="parsedSecret">The received secret.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A validation result
    /// </returns>
    /// <exception cref="System.ArgumentException">id or credential is missing.</exception>
    /// <inheritdoc/>
    public Task<SecretValidationResult> ValidateAsync(IEnumerable<Secret> secrets, ParsedSecret parsedSecret, Ct ct)
    {
        var fail = Task.FromResult(new SecretValidationResult { Success = false });
        var success = Task.FromResult(new SecretValidationResult { Success = true });

        if (parsedSecret.Type != IdentityServerConstants.ParsedSecretTypes.SharedSecret)
        {
            _logger.LogError("Parsed secret should not be of type: {type}", parsedSecret.Type ?? "null");
            return fail;
        }

        var sharedSecrets = secrets.Where(s => s.Type == IdentityServerConstants.SecretTypes.SharedSecret);
        if (!sharedSecrets.Any())
        {
            _logger.LogDebug("No shared secret configured for client.");
            return fail;
        }

        var sharedSecret = parsedSecret.Credential as string;

        if (parsedSecret.Id.IsMissing() || sharedSecret.IsMissing())
        {
            throw new ArgumentException("Id or Credential is missing.");
        }

        foreach (var secret in sharedSecrets)
        {
            // use time constant string comparison
            var isValid = TimeConstantComparer.IsEqual(sharedSecret, secret.Value);

            if (isValid)
            {
                return success;
            }
        }

        _logger.LogDebug("No matching plain text secret found.");
        return fail;
    }
}
