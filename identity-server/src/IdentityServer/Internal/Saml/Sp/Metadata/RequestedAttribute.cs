// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    /// <summary>
    /// Specifies an attribute requested by the service provider.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
    internal class RequestedAttribute : Saml2Attribute
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="name">Name of the attribute.</param>
        public RequestedAttribute(string name)
            : base(name)
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="name">Name of the attribute.</param>
        /// <param name="value">Value of the attribute.</param>
        public RequestedAttribute(string name, string value)
            : base(name, value)
        { }

        /// <summary>
        /// Is this attribute required by the service provider?
        /// </summary>
        public bool? IsRequired { get; set; }

        /// <summary>
        /// Uri used for NameFormat to specify that the Name is a Uri.
        /// </summary>
        public static readonly Uri AttributeNameFormatUri = new Uri("urn:oasis:names:tc:SAML:2.0:attrname-format:uri");

        /// <summary>
        /// Uri used for NameFormat to specify that the format of the Name 
        /// is unspecified.
        /// </summary>
        public static readonly Uri AttributeNameFormatUnspecified = new Uri("urn:oasis:names:tc:SAML:2.0:attrname-format:unspecified");

        /// <summary>
        /// Uri used for NameFormat to specify that the format of the Name 
        /// fulfills the standard's basic requirements.
        /// </summary>
        public static readonly Uri AttributeNameFormatBasic = new Uri("urn:oasis:names:tc:SAML:2.0:attrname-format:basic");
    }
}
