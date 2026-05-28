// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;

namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    static class StatusCodeHelper
    {
        static readonly IDictionary<string, Saml2StatusCode> fromString =
            new Dictionary<string, Saml2StatusCode>()
        {
            { "urn:oasis:names:tc:SAML:2.0:status:Success", Saml2StatusCode.Success },
            { "urn:oasis:names:tc:SAML:2.0:status:Requester", Saml2StatusCode.Requester },
            { "urn:oasis:names:tc:SAML:2.0:status:Responder", Saml2StatusCode.Responder },
            { "urn:oasis:names:tc:SAML:2.0:status:VersionMismatch", Saml2StatusCode.VersionMismatch },
            { "urn:oasis:names:tc:SAML:2.0:status:AuthnFailed", Saml2StatusCode.AuthnFailed },
            { "urn:oasis:names:tc:SAML:2.0:status:InvalidAttrNameOrValue", Saml2StatusCode.InvalidAttrNameOrValue },
            { "urn:oasis:names:tc:SAML:2.0:status:InvalidNameIDPolicy", Saml2StatusCode.InvalidNameIdPolicy },
            { "urn:oasis:names:tc:SAML:2.0:status:NoAuthnContext", Saml2StatusCode.NoAuthnContext },
            { "urn:oasis:names:tc:SAML:2.0:status:NoAvailableIDP", Saml2StatusCode.NoAvailableIdp },
            { "urn:oasis:names:tc:SAML:2.0:status:NoPassive", Saml2StatusCode.NoPassive },
            { "urn:oasis:names:tc:SAML:2.0:status:NoSupportedIDP", Saml2StatusCode.NoSupportedIdp },
            { "urn:oasis:names:tc:SAML:2.0:status:PartialLogout", Saml2StatusCode.PartialLogout },
            { "urn:oasis:names:tc:SAML:2.0:status:ProxyCountExceeded", Saml2StatusCode.ProxyCountExceeded },
            { "urn:oasis:names:tc:SAML:2.0:status:RequestDenied", Saml2StatusCode.RequestDenied },
            { "urn:oasis:names:tc:SAML:2.0:status:RequestUnsupported", Saml2StatusCode.RequestUnsupported },
            { "urn:oasis:names:tc:SAML:2.0:status:RequestVersionDeprecated", Saml2StatusCode.RequestVersionDeprecated },
            { "urn:oasis:names:tc:SAML:2.0:status:RequestVersionTooHigh", Saml2StatusCode.RequestVersionTooHigh },
            { "urn:oasis:names:tc:SAML:2.0:status:RequestVersionTooLow", Saml2StatusCode.RequestVersionTooLow },
            { "urn:oasis:names:tc:SAML:2.0:status:ResourceNotRecognized", Saml2StatusCode.ResourceNotRecognized },
            { "urn:oasis:names:tc:SAML:2.0:status:TooManyResponses", Saml2StatusCode.TooManyResponses },
            { "urn:oasis:names:tc:SAML:2.0:status:UnknownAttrProfile", Saml2StatusCode.UnknownAttrProfile },
            { "urn:oasis:names:tc:SAML:2.0:status:UnknownPrincipal", Saml2StatusCode.UnknownPrincipal },
            { "urn:oasis:names:tc:SAML:2.0:status:UnsupportedBinding", Saml2StatusCode.UnsupportedBinding },
        };

        static readonly IDictionary<Saml2StatusCode, string> fromCode =
            fromString.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        public static Saml2StatusCode FromString(string statusString)
        {
            return fromString[statusString];
        }

        public static string FromCode(Saml2StatusCode statusCode)
        {
            return fromCode[statusCode];
        }
    }
}
