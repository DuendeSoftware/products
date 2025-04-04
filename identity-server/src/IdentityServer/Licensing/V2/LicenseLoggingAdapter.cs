// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2;

internal class LicenseLoggingAdapter(bool isRedistribution, ILogger logger)
{
    public void LogError(string message)
    {
        if (isRedistribution)
        {
            logger.LogTrace(message);
        }
        else
        {
            logger.LogError(message);
        }
    }
}
