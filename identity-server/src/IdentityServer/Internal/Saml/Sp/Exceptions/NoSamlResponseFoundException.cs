// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Runtime.Serialization;

namespace Duende.IdentityServer.Internal.Saml.Sp.Exceptions
{
    /// <summary>
    /// No saml response was found in the http request.
    /// </summary>
    [Serializable]
    internal class NoSamlResponseFoundException : Saml2Exception
    {
        /// <summary>
        /// Default Ctor, setting message to a default.
        /// </summary>
        public NoSamlResponseFoundException()
            : this("No Saml2 Response found in the http request.")
        {
        }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        public NoSamlResponseFoundException(string message)
            : base(message)
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        /// <param name="innerException">Inner exception.</param>
        public NoSamlResponseFoundException(string message, Exception innerException)
            : base(message, innerException)
        { }

        /// <summary>
        /// Serialization Ctor
        /// </summary>
        /// <param name="info">Serialization info</param>
        /// <param name="context">Serialization context</param>
        protected NoSamlResponseFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
