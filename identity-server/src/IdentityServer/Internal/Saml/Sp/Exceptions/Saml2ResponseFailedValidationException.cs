// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Runtime.Serialization;

namespace Duende.IdentityServer.Internal.Saml.Sp.Exceptions
{
    /// <summary>
    /// A SAML2 Response failed validation.
    /// </summary>
    [Serializable]
    internal class Saml2ResponseFailedValidationException : Saml2Exception
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public Saml2ResponseFailedValidationException()
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        public Saml2ResponseFailedValidationException(string message) : base(message)
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        /// <param name="innerException">Inner exception.</param>
        public Saml2ResponseFailedValidationException(string message, Exception innerException)
            : base(message, innerException)
        { }

        /// <summary>
        /// Serialization Ctor
        /// </summary>
        /// <param name="info">Serialization info</param>
        /// <param name="context">Serialization context</param>
        protected Saml2ResponseFailedValidationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        { }
    }
}
