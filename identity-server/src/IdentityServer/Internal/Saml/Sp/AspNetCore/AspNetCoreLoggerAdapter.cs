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
            logger.AdapterError(message, ex);
        }

        /// <InheritDoc />
        public void WriteInformation(string message)
        {
            logger.AdapterInformation(message);
        }

        /// <InheritDoc />
        public void WriteVerbose(string message)
        {
            logger.AdapterVerbose(message);
        }
    }

    internal static partial class Log
    {
        [LoggerMessage(
            EventName = nameof(AdapterError),
            Level = LogLevel.Error,
            Message = "{Message}")]
        internal static partial void AdapterError(this ILogger logger, string message, Exception ex);

        [LoggerMessage(
            EventName = nameof(AdapterInformation),
            Level = LogLevel.Information,
            Message = "{Message}")]
        internal static partial void AdapterInformation(this ILogger logger, string message);

        [LoggerMessage(
            EventName = nameof(AdapterVerbose),
            Level = LogLevel.Debug,
            Message = "{Message}")]
        internal static partial void AdapterVerbose(this ILogger logger, string message);
    }
}
