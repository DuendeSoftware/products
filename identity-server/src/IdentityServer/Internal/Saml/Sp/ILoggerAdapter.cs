// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Interface for an adapter around the logging framework used on each
    /// platform.
    /// </summary>
    internal interface ILoggerAdapter
    {
        /// <summary>
        /// Write informational message.
        /// </summary>
        /// <param name="message">Message to write.</param>
        void WriteInformation(string message);

        /// <summary>
        /// Write an error message
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="ex">Exception to include in error message.</param>
        void WriteError(string message, Exception ex);

        /// <summary>
        /// Write an informational message on the verbose level.
        /// </summary>
        /// <param name="message">Message to write</param>
        void WriteVerbose(string message);
    }
}
