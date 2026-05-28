// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Runtime.Serialization;

namespace Duende.IdentityServer.Internal.Saml.Sp.Exceptions
{
    /// <summary>
    /// Base class for authentication services specific exceptions, that might                     
    /// require special handling for error reporting to the user.
    /// </summary>
    [Serializable]
    internal abstract class Saml2Exception : Exception
    {
        /// <summary>
        /// Default Ctor
        /// </summary>
        protected Saml2Exception() { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        protected Saml2Exception(string message)
            : base(message)
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message">Message of the exception.</param>
        /// <param name="innerException">Inner exception.</param>
        protected Saml2Exception(string message, Exception innerException)
            : base(message, innerException)
        { }

        /// <summary>
        /// Serialization Ctor
        /// </summary>
        /// <param name="info">Serialization info</param>
        /// <param name="context">Serialization context</param>
        protected Saml2Exception(SerializationInfo info, StreamingContext context)
            : base(info, context)
        { }
    }
}
