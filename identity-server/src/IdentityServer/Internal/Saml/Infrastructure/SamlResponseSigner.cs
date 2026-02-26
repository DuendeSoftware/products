// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml.Linq;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal class SamlResponseSigner(
    ISamlSigningService samlSigningService,
    IOptions<SamlOptions> samlOptions,
    ILogger<SamlResponseSigner> logger)
{
    internal async Task<string> SignResponse(XElement responseElement, SamlServiceProvider serviceProvider, Ct ct)
    {
        var signingBehavior = serviceProvider.SigningBehavior ?? samlOptions.Value.DefaultSigningBehavior;

        if (signingBehavior == SamlSigningBehavior.DoNotSign)
        {
            logger.SigningDisabledForServiceProvider(LogLevel.Debug, serviceProvider.EntityId);
            return responseElement.ToString(SaveOptions.DisableFormatting);
        }

        var certificate = await samlSigningService.GetSigningCertificateAsync(ct);

        logger.SigningSamlResponse(LogLevel.Debug, serviceProvider.EntityId, signingBehavior);

        try
        {
            var signedXml = signingBehavior switch
            {
                SamlSigningBehavior.SignResponse =>
                    XmlSignatureHelper.SignResponse(responseElement, certificate),

                SamlSigningBehavior.SignAssertion =>
                    XmlSignatureHelper.SignAssertionInResponse(responseElement, certificate),

                SamlSigningBehavior.SignBoth =>
                    XmlSignatureHelper.SignBoth(responseElement, certificate),

                _ => throw new ArgumentException($"Unknown signing behavior: {signingBehavior}")
            };

            logger.SuccessfullySignedSamlResponse(LogLevel.Debug, serviceProvider.EntityId);

            return signedXml;
        }
        catch (Exception ex)
        {
            logger.FailedToSignSamlResponse(ex, serviceProvider.EntityId, ex.Message);
            throw;
        }
    }
}
