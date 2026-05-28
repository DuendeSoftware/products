// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Tokens
{
    internal static class SecurityAlgorithms
    {
        public const string Aes128Encryption = "http://www.w3.org/2001/04/xmlenc#aes128-cbc";
        public const string Aes128KeyWrap = "http://www.w3.org/2001/04/xmlenc#kw-aes128";
        public const string Aes192Encryption = "http://www.w3.org/2001/04/xmlenc#aes192-cbc";
        public const string Aes192KeyWrap = "http://www.w3.org/2001/04/xmlenc#kw-aes192";
        public const string Aes256Encryption = "http://www.w3.org/2001/04/xmlenc#aes256-cbc";
        public const string Aes256KeyWrap = "http://www.w3.org/2001/04/xmlenc#kw-aes256";
        public const string DesEncryption = "http://www.w3.org/2001/04/xmlenc#des-cbc";
        public const string DsaSha1Signature = "http://www.w3.org/2000/09/xmldsig#dsa-sha1";
        public const string ExclusiveC14n = "http://www.w3.org/2001/10/xml-exc-c14n#";
        public const string ExclusiveC14nWithComments = "http://www.w3.org/2001/10/xml-exc-c14n#WithComments";
        public const string HmacSha1Signature = "http://www.w3.org/2000/09/xmldsig#hmac-sha1";
        public const string Psha1KeyDerivation = "http://schemas.xmlsoap.org/ws/2005/02/sc/dk/p_sha1";
        public const string Ripemd160Digest = "http://www.w3.org/2001/04/xmlenc#ripemd160";
        public const string RsaOaepKeyWrap = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";
        public const string RsaSha1Signature = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";
        public const string RsaV15KeyWrap = "http://www.w3.org/2001/04/xmlenc#rsa-1_5";
        public const string Sha1Digest = "http://www.w3.org/2000/09/xmldsig#sha1";
        public const string Sha256Digest = "http://www.w3.org/2001/04/xmlenc#sha256";
        public const string Sha512Digest = "http://www.w3.org/2001/04/xmlenc#sha512";
        public const string TripleDesEncryption = "http://www.w3.org/2001/04/xmlenc#tripledes-cbc";
        public const string TripleDesKeyWrap = "http://www.w3.org/2001/04/xmlenc#kw-tripledes";
        public const string HmacSha256Signature = "http://www.w3.org/2001/04/xmldsig-more#hmac-sha256";
        public const string RsaSha256Signature = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
        public const string RsaSha384Signature = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha384";
        public const string RsaSha512Signature = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha512";
        public const string TlsSspiKeyWrap = "http://schemas.xmlsoap.org/2005/02/trust/tlsnego#TLS_Wrap";
        public const string WindowsSspiKeyWrap = "http://schemas.xmlsoap.org/2005/02/trust/spnego#GSS_Wrap";

        public const string EcDsaSha1 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha1";
        public const string EcDsaSha224 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha224";
        public const string EcDsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256";
        public const string EcDsaSha384 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha384";
        public const string EcDsaSha512 = "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha512";
    }
}
