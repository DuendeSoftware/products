// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Logger adapter that does nothing.
    /// </summary>
    internal class NullLoggerAdapter : ILoggerAdapter
    {
        /// <summary>
        /// Write an error message
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="ex">Exception to include in error message.</param>
        public void WriteError(string message, Exception ex)
        {
        }

        /// <summary>
        /// Write informational message.
        /// </summary>
        /// <param name="message">Message to write.</param>
        public void WriteInformation(string message)
        {
        }

        /// <summary>
        /// Write an informational message on the verbose level.
        /// </summary>
        /// <param name="message">Message to write</param>
        public void WriteVerbose(string message)
        {
        }
    }
}
