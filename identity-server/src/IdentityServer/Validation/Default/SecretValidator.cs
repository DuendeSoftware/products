// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validates secrets using the registered validators
/// </summary>
public class SecretValidator : ISecretsListValidator
{
    private readonly ILogger _logger;
    private readonly IEnumerable<ISecretValidator> _validators;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretValidator"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="validators">The validators.</param>
    /// <param name="logger">The logger.</param>
    public SecretValidator(TimeProvider timeProvider, IEnumerable<ISecretValidator> validators, ILogger<ISecretsListValidator> logger)
    {
        _timeProvider = timeProvider;
        _validators = validators;
        _logger = logger;
    }

    /// <summary>
    /// Validates the secret.
    /// </summary>
    /// <param name="parsedSecret">The parsed secret.</param>
    /// <param name="secrets">The secrets.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    /// <inheritdoc/>
    public async Task<SecretValidationResult> ValidateAsync(IEnumerable<Secret> secrets, ParsedSecret parsedSecret, Ct ct)
    {
        var secretsArray = secrets as Secret[] ?? secrets.ToArray();

        foreach (var expired in secretsArray.Where(s => s.Expiration.HasExpired(_timeProvider.GetUtcNow().UtcDateTime)))
        {
            _logger.LogInformation("Secret [{description}] is expired", expired.Description ?? "no description");
        }

        var currentSecrets = secretsArray.Where(s => !s.Expiration.HasExpired(_timeProvider.GetUtcNow().UtcDateTime)).ToArray();

        // see if a registered validator can validate the secret
        foreach (var validator in _validators)
        {
            var secretValidationResult = await validator.ValidateAsync(currentSecrets, parsedSecret, ct);

            if (secretValidationResult.Success)
            {
                _logger.LogDebug("Secret validator success: {0}", validator.GetType().Name);
                return secretValidationResult;
            }
        }

        _logger.LogDebug("Secret validators could not validate secret");
        return new SecretValidationResult { Success = false };
    }
}
