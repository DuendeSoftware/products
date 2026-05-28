// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.Sp.AspNetCore
{
    /// <summary>
    /// Logger adapter for ASP.NET Core
    /// </summary>
    internal class AspNetCoreLoggerAdapter : ILoggerAdapter
    {
        private ILogger logger;

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="logger">Logger to write to</param>
        public AspNetCoreLoggerAdapter(ILogger logger)
        {
            this.logger = logger;
        }

        /// <InheritDoc />
        public void WriteError(string message, Exception ex)
        {
            logger.LogError(ex, message);
        }

        /// <InheritDoc />
        public void WriteInformation(string message)
        {
            logger.LogInformation(message);
        }

        /// <InheritDoc />
        public void WriteVerbose(string message)
        {
            logger.LogDebug(message);
        }
    }
}
