// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport;

/// <summary>
/// Signing algorithm constants used for conformance assessment.
/// These match the values from Microsoft.IdentityModel.Tokens.SecurityAlgorithms.
/// </summary>
internal static class SigningAlgorithms
{
    // HMAC algorithms (symmetric - considered insecure for OAuth 2.1)
    public const string HmacSha256 = "http://www.w3.org/2001/04/xmldsig-more#hmac-sha256";
    public const string HmacSha384 = "http://www.w3.org/2001/04/xmldsig-more#hmac-sha384";
    public const string HmacSha512 = "http://www.w3.org/2001/04/xmldsig-more#hmac-sha512";

    // RSA algorithms
    public const string RsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    public const string RsaSha384 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha384";
    public const string RsaSha512 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha512";

    // RSA-PSS algorithms (FAPI 2.0 compliant)
    public const string RsaSsaPssSha256 = "http://www.w3.org/2007/05/xmldsig-more#sha256-rsa-MGF1";
    public const string RsaSsaPssSha384 = "http://www.w3.org/2007/05/xmldsig-more#sha384-rsa-MGF1";
    public const string RsaSsaPssSha512 = "http://www.w3.org/2007/05/xmldsig-more#sha512-rsa-MGF1";

    // ECDSA algorithms (FAPI 2.0 compliant)
    public const string EcdsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";
    public const string EcdsaSha384 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha384";
    public const string EcdsaSha512 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha512";
}
