// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validator for an X.509 certificate based client secret using the common name
/// </summary>
public class X509NameSecretValidator : ISecretValidator
{
    private readonly ILogger<X509NameSecretValidator> _logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="logger"></param>
    public X509NameSecretValidator(ILogger<X509NameSecretValidator> logger) => _logger = logger;

    /// <inheritdoc/>
    /// <inheritdoc/>
    public Task<SecretValidationResult> ValidateAsync(IEnumerable<Secret> secrets, ParsedSecret parsedSecret, Ct ct)
    {
        var fail = Task.FromResult(new SecretValidationResult { Success = false });

        if (parsedSecret.Type != IdentityServerConstants.ParsedSecretTypes.X509Certificate)
        {
            _logger.LogDebug("X509 name secret validator cannot process {type}", parsedSecret.Type ?? "null");
            return fail;
        }

        if (!(parsedSecret.Credential is X509Certificate2 cert))
        {
            throw new InvalidOperationException("Credential is not a x509 certificate.");
        }

        var name = cert.Subject;
        if (name == null)
        {
            _logger.LogWarning("No subject/name found in X509 certificate.");
            return fail;
        }

        var nameSecrets = secrets.Where(s => s.Type == IdentityServerConstants.SecretTypes.X509CertificateName);
        if (!nameSecrets.Any())
        {
            _logger.LogDebug("No x509 name secrets configured for client.");
            return fail;
        }

        foreach (var nameSecret in nameSecrets)
        {
            if (name.Equals(nameSecret.Value, StringComparison.Ordinal))
            {
                var result = new SecretValidationResult
                {
                    Success = true,
                    Confirmation = cert.CreateThumbprintCnf()
                };

                return Task.FromResult(result);
            }
        }

        _logger.LogDebug("No matching x509 name secret found.");
        return fail;
    }
}
