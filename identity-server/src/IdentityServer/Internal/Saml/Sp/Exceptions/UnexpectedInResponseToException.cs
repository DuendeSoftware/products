// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Runtime.Serialization;

namespace Duende.IdentityServer.Internal.Saml.Sp.Exceptions
{
    /// <summary>
    /// A SAML2 Response failed InResponseTo validation because RelayState is lost, or an unsolicited response contains an InResponseTo
    /// </summary>
    [Serializable]
    internal class UnexpectedInResponseToException : Saml2ResponseFailedValidationException
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public UnexpectedInResponseToException()
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        public UnexpectedInResponseToException(string message) : base(message)
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        /// <param name="innerException">Inner exception.</param>
        public UnexpectedInResponseToException(string message, Exception innerException)
            : base(message, innerException)
        { }

        /// <summary>
        /// Serialization Ctor
        /// </summary>
        /// <param name="info">Serialization info</param>
        /// <param name="context">Serialization context</param>
        protected UnexpectedInResponseToException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        { }
    }
}
