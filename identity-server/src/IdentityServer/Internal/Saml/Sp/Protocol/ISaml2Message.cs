// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// Common properties of all Saml2 message implementations (both requests
    /// and responses). There is no corresponding definition in the SAML2
    /// standard, so this is made up of the common fields of 3.2.2 Complex Type
    /// StatusResponseType (the base type for all responses) and of 3.2.1 Complex
    /// Type RequestAbstractType.
    /// </summary>
    internal interface ISaml2Message
    {
        /// <summary>
        /// The destination of the message.
        /// </summary>
        Uri DestinationUrl { get; }

        /// <summary>
        /// Serializes the message into wellformed Xml.
        /// </summary>
        /// <returns>string containing the Xml data.</returns>
        string ToXml();

        /// <summary>
        /// Transforms the message to an XElement object tree.
        /// </summary>
        /// <returns>XElement with Xml representation of the message</returns>
        XElement ToXElement();

        /// <summary>
        /// The name of the message to use in a query string or form input
        /// field. Typically "SAMLRequest" or "SAMLResponse".
        /// </summary>
        string MessageName { get; }

        /// <summary>
        /// RelayState attached to the message.
        /// </summary>
        /// <remarks>Strictly speaking, this is not part of the message,
        /// but it is delivered together with the message so we need to keep
        /// track of it together with a message.</remarks>
        string RelayState { get; }

        /// <summary>
        /// Certificate used to sign the message with during binding, according
        /// to the signature processing rules of each binding.
        /// </summary>
        X509Certificate2 SigningCertificate { get; }

        /// <summary>
        /// The signing algorithm to use when signing the message during binding, 
        /// according to the signature processing rules of each binding.
        /// </summary>
        /// <value>The signing algorithm.</value>
        string SigningAlgorithm { get; }

        /// <summary>
        /// Issuer of the message.
        /// </summary>
        EntityId Issuer { get; }
    }
}
