// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Net;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
namespace Duende.IdentityServer.Internal.Saml.Sp.Commands
{
    /// <summary>
    /// Represents a missing command.
    /// Instances of this class are returned by CommandFactory.GetCommand(...)
    /// when the specified command name is not recognised.
    /// </summary>
    internal class NotFoundCommand : ICommand
    {
        /// <summary>
        /// Run the command, returning a CommandResult specifying an HTTP 404 Not Found status code.
        /// </summary>
        /// <param name="request">Request data.</param>
        /// <param name="options">Options</param>
        /// <param name="timeProvider">The time provider.</param>
        /// <returns>CommandResult</returns>
        public CommandResult Run(HttpRequestData request, IOptions options, TimeProvider timeProvider)
        {
            return new CommandResult()
            {
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
    }
}
