// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
namespace Duende.IdentityServer.Internal.Saml.Sp.Commands
{
    /// <summary>
    /// A command - corresponds to an action in Mvc.
    /// </summary>
    internal interface ICommand
    {
        /// <summary>
        /// Run the command and return a result.
        /// </summary>
        /// <param name="request">The http request that the input
        /// data can be read from.</param>
        /// <param name="options">The options to use when performing the command.</param>
        /// <param name="timeProvider">The time provider to use for time-based operations.</param>
        /// <returns>The results of the command, as a DTO.</returns>
        CommandResult Run(HttpRequestData request, IOptions options, TimeProvider timeProvider);
    }
}
