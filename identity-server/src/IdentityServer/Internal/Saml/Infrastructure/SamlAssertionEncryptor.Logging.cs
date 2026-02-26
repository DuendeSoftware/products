// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal static partial class SamlAssertionEncryptorLoggingExtensions
{
    private static class SamlAssertionEncryptorLogParameters
    {
        public const string EntityId = "entityId";
        public const string ExpirationDate = "expirationDate";
        public const string ValidFrom = "validFrom";
        public const string KeySize = "keySize";
        public const string ErrorMessage = "errorMessage";
    }

    [LoggerMessage(
        EventName = nameof(EncryptingAssertion),
        Message = $"Encrypting SAML assertion for service provider {{{SamlAssertionEncryptorLogParameters.EntityId}}}"
    )]
    internal static partial void EncryptingAssertion(this ILogger logger, LogLevel level, string entityId);

    [LoggerMessage(
        EventName = nameof(AssertionEncryptedSuccessfully),
        Message = $"Successfully encrypted SAML assertion for service provider {{{SamlAssertionEncryptorLogParameters.EntityId}}}"
    )]
    internal static partial void AssertionEncryptedSuccessfully(this ILogger logger, LogLevel level, string entityId);

    [LoggerMessage(
        EventName = nameof(CertificateExpired),
        Message = $"Encryption certificate for service provider {{{SamlAssertionEncryptorLogParameters.EntityId}}} has expired (expiration: {{{SamlAssertionEncryptorLogParameters.ExpirationDate}}})"
    )]
    internal static partial void CertificateExpired(this ILogger logger, LogLevel level, string entityId, DateTime expirationDate);

    [LoggerMessage(
        EventName = nameof(CertificateNotYetValid),
        Message = $"Encryption certificate for service provider {{{SamlAssertionEncryptorLogParameters.EntityId}}} is not yet valid (valid from: {{{SamlAssertionEncryptorLogParameters.ValidFrom}}})"
    )]
    internal static partial void CertificateNotYetValid(this ILogger logger, LogLevel level, string entityId, DateTime validFrom);

    [LoggerMessage(
        EventName = nameof(CertificateHasNoPublicRsaKey),
        Message = $"Encryption certificate for service provider {{{SamlAssertionEncryptorLogParameters.EntityId}}} has no public RSA key")]
    internal static partial void CertificateHasNoPublicRsaKey(this ILogger logger, LogLevel level, string entityId);

    [LoggerMessage(
        EventName = nameof(CertificateWeakKeySize),
        Message = $"Encryption certificate for service provider {{{SamlAssertionEncryptorLogParameters.EntityId}}} has weak RSA key size ({{{SamlAssertionEncryptorLogParameters.KeySize}}} bits). Minimum required: 2048 bits"
    )]
    internal static partial void CertificateWeakKeySize(this ILogger logger, LogLevel level, string entityId, int keySize);

    [LoggerMessage(
        EventName = nameof(CertificateValidated),
        Message = $"Encryption certificate for service provider {{{SamlAssertionEncryptorLogParameters.EntityId}}} validated successfully (expires: {{{SamlAssertionEncryptorLogParameters.ExpirationDate}}})"
    )]
    internal static partial void CertificateValidated(this ILogger logger, LogLevel level, string entityId, DateTime expirationDate);

    [LoggerMessage(
        EventName = nameof(FailedToEncryptAssertion),
        Level = LogLevel.Error,
        Message = $"Failed to encrypt SAML assertion for service provider {{{SamlAssertionEncryptorLogParameters.EntityId}}}: {{{SamlAssertionEncryptorLogParameters.ErrorMessage}}}"
    )]
    internal static partial void FailedToEncryptAssertion(this ILogger logger, Exception exception, string entityId, string errorMessage);
}
