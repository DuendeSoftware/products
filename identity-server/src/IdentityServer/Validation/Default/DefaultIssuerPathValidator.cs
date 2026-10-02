// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

public class DefaultIssuerPathValidator(IIssuerNameService issuerNameService, ILogger<DefaultIssuerPathValidator> logger) : IIssuerPathValidator
{
    public async Task<bool> ValidateAsync(string path, Ct ct)
    {
        //if there is no path, this is fine since the default issuer is probably being used
        if (path.IsMissing())
        {
            return true;
        }

        //if there is a path, then we should be matching against an explicitly configured issuer
        var currentIssuer = await issuerNameService.GetCurrentAsync(ct);
        if (!Uri.TryCreate(currentIssuer, UriKind.Absolute, out var uri))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.CurrentIssuerIsNotAValidAbsoluteURI(currentIssuer.SanitizeLogParameter());
            }
            return false;
        }

        if (!string.Equals(uri.AbsolutePath, path, StringComparison.OrdinalIgnoreCase))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.CurrentIssuerPathDoesNotMatchTheProvided(uri.AbsolutePath.SanitizeLogParameter(), path.SanitizeLogParameter());
            }
            return false;
        }

        return true;
    }
}
