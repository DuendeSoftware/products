// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Runtime.Serialization;

namespace Duende.IdentityServer.Internal.Saml.Sp.Exceptions
{
    /// <summary>
    /// Exception thrown when an signature is not valid according to the
    /// SAML standard.
    /// </summary>
    [Serializable]
    internal class InvalidSignatureException : Saml2Exception
    {
        /// <summary>
        /// Default ctor
        /// </summary>
        public InvalidSignatureException() { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message of exception</param>
        public InvalidSignatureException(string message)
            : base(message)
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="innerException">Inner exception</param>
        public InvalidSignatureException(string message, Exception innerException)
            : base(message, innerException)
        { }

        /// <summary>
        /// Serialization Ctor
        /// </summary>
        /// <param name="info">Serialization info</param>
        /// <param name="context">Serialization context</param>
        protected InvalidSignatureException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        { }
    }
}
