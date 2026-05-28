// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Runtime.Serialization;

namespace Duende.IdentityServer.Internal.Saml.Sp.Exceptions
{
    /// <summary>
    /// A SAML response was found, but could not be parsed due to formatting issues.
    /// </summary>
    [Serializable]
    internal class BadFormatSamlResponseException : Saml2Exception
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public BadFormatSamlResponseException()
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        public BadFormatSamlResponseException(string message) : base(message)
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        /// <param name="innerException">Inner exception.</param>
        public BadFormatSamlResponseException(string message, Exception innerException)
            : base(message, innerException)
        { }

        /// <summary>
        /// Serialization Ctor
        /// </summary>
        /// <param name="info">Serialization info</param>
        /// <param name="context">Serialization context</param>
        protected BadFormatSamlResponseException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        { }
    }
}
