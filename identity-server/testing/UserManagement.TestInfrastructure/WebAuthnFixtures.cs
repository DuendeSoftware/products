// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;

namespace Duende.UserManagement;

public static class WebAuthnFixtures
{
#pragma warning disable CA1054 // URI parameters should not be strings
    public static byte[] DecodeBase64Url(string base64Url)
#pragma warning restore CA1054
    {
        var buffer = new byte[256];
        Base64Url.TryDecodeFromChars(base64Url, buffer, out var bytesWritten).ShouldBeTrue();
        return buffer[..bytesWritten];
    }

    public static string CreateClientDataJson(string type, string challenge, string origin)
    {
        var json = JsonSerializer.Serialize(new { type, challenge, origin });
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));
    }

    internal static string CreateClientDataJsonWithCrossOrigin(string type, string challenge, string origin,
        string? topOrigin = null)
    {
        var json = JsonSerializer.Serialize(new
        {
            type,
            challenge,
            origin,
            crossOrigin = true,
            topOrigin
        });
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));
    }

    public static byte[] CreateAuthenticatorData(string rpId, byte flags = 0x01, uint signCount = 0)
    {
        var rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
        var signCountBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(signCountBytes, signCount);

        var authData = new byte[37];
        rpIdHash.CopyTo(authData, 0);
        authData[32] = flags;
        signCountBytes.CopyTo(authData, 33);

        return authData;
    }

    internal static byte[] CreateCoseKey(int algorithm = -7)
    {
        var writer = new CborWriter();
        writer.WriteStartMap(5);

        // kty (key type) = 2 (EC2)
        writer.WriteInt32(1);
        writer.WriteInt32(2);

        // alg (algorithm)
        writer.WriteInt32(3);
        writer.WriteInt32(algorithm);

        // crv = 1 (P-256)
        writer.WriteInt32(-1);
        writer.WriteInt32(1);

        // x coordinate (32 bytes)
        writer.WriteInt32(-2);
        writer.WriteByteString(RandomNumberGenerator.GetBytes(32));

        // y coordinate (32 bytes)
        writer.WriteInt32(-3);
        writer.WriteByteString(RandomNumberGenerator.GetBytes(32));

        writer.WriteEndMap();
        return writer.Encode();
    }

    internal static byte[] CreateCoseKeyFromEcdsa(ECDsa ecdsa)
    {
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        var writer = new CborWriter();
        writer.WriteStartMap(5);
        writer.WriteInt32(1); // kty
        writer.WriteInt32(2); // EC2
        writer.WriteInt32(3); // alg
        writer.WriteInt32(-7); // ES256
        writer.WriteInt32(-1); // crv
        writer.WriteInt32(1); // P-256
        writer.WriteInt32(-2); // x
        writer.WriteByteString(parameters.Q.X!);
        writer.WriteInt32(-3); // y
        writer.WriteByteString(parameters.Q.Y!);
        writer.WriteEndMap();
        return writer.Encode();
    }

    internal static byte[] CreateCoseKeyWithAlgorithm(int algorithm)
    {
        // Create a minimal COSE key with the specified algorithm (OKP key type for EdDSA)
        var writer = new CborWriter();
        writer.WriteStartMap(3);

        // kty (key type)
        writer.WriteInt32(1);
        writer.WriteInt32(1); // OKP

        // alg (algorithm)
        writer.WriteInt32(3);
        writer.WriteInt32(algorithm);

        // x coordinate (for OKP/EdDSA, this is the public key)
        writer.WriteInt32(-2);
        writer.WriteByteString(RandomNumberGenerator.GetBytes(32));

        writer.WriteEndMap();
        return writer.Encode();
    }

    internal static byte[] CreateAttestationObject(
        string format,
        string rpId,
        byte[]? credentialId = null,
        byte? flags = null,
        bool includeCredentialData = true,
        int algorithm = -7)
    {
        credentialId ??= RandomNumberGenerator.GetBytes(32);

        var relyingPartyIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));

        // Default flags: UP (0x01) + AT (0x40) for attested credential data present
        var actualFlags = flags ?? (byte)(0x01 | 0x40);

        var signCount = new byte[4]; // 0

        byte[] authData;
        if (includeCredentialData)
        {
            var coseKey = CreateCoseKey(algorithm);

            var aaguid = new byte[16];
            var credIdLength = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(credIdLength, (ushort)credentialId.Length);

            authData = new byte[37 + 16 + 2 + credentialId.Length + coseKey.Length];
            var offset = 0;
            relyingPartyIdHash.CopyTo(authData, offset);
            offset += 32;
            authData[offset++] = actualFlags;
            signCount.CopyTo(authData, offset);
            offset += 4;
            aaguid.CopyTo(authData, offset);
            offset += 16;
            credIdLength.CopyTo(authData, offset);
            offset += 2;
            credentialId.CopyTo(authData, offset);
            offset += credentialId.Length;
            coseKey.CopyTo(authData, offset);
        }
        else
        {
            // Minimal auth data without attested credential data
            // Clear AT flag since no credential data is present
            actualFlags = (byte)(actualFlags & ~0x40);
            authData = new byte[37];
            var offset = 0;
            relyingPartyIdHash.CopyTo(authData, offset);
            offset += 32;
            authData[offset++] = actualFlags;
            signCount.CopyTo(authData, offset);
        }

        var writer = new CborWriter();
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString(format);
        writer.WriteTextString("authData");
        writer.WriteByteString(authData);
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteEndMap();

        return writer.Encode();
    }

    public static byte[] CreateAttestationObjectWithEcdsa(
        string format, string rpId, byte[] credentialId, ECDsa ecdsa, byte flags = 0x41, Guid? aaguid = null) // UP + AT
    {
        ArgumentNullException.ThrowIfNull(credentialId);
        ArgumentNullException.ThrowIfNull(ecdsa);

        var relyingPartyIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
        var signCount = new byte[4];

        var coseKey = CreateCoseKeyFromEcdsa(ecdsa);
        var aaguidBytes = (aaguid ?? Guid.Empty).ToByteArray(bigEndian: true);
        var credIdLength = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(credIdLength, (ushort)credentialId.Length);

        var authData = new byte[37 + 16 + 2 + credentialId.Length + coseKey.Length];
        var offset = 0;
        relyingPartyIdHash.CopyTo(authData, offset);
        offset += 32;
        authData[offset++] = flags;
        signCount.CopyTo(authData, offset);
        offset += 4;
        aaguidBytes.CopyTo(authData, offset);
        offset += 16;
        credIdLength.CopyTo(authData, offset);
        offset += 2;
        credentialId.CopyTo(authData, offset);
        offset += credentialId.Length;
        coseKey.CopyTo(authData, offset);

        var writer = new CborWriter();
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString(format);
        writer.WriteTextString("authData");
        writer.WriteByteString(authData);
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteEndMap();

        return writer.Encode();
    }

    internal static byte[] CreateAttestationObjectWithAlgorithm(
        string format, string rpId, byte[] credentialId, int algorithm)
    {
        var relyingPartyIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
        var flags = (byte)(0x01 | 0x40); // UP + AT
        var signCount = new byte[4];

        var coseKey = CreateCoseKeyWithAlgorithm(algorithm);
        var aaguid = new byte[16];
        var credIdLength = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(credIdLength, (ushort)credentialId.Length);

        var authData = new byte[37 + 16 + 2 + credentialId.Length + coseKey.Length];
        var offset = 0;
        relyingPartyIdHash.CopyTo(authData, offset);
        offset += 32;
        authData[offset++] = flags;
        signCount.CopyTo(authData, offset);
        offset += 4;
        aaguid.CopyTo(authData, offset);
        offset += 16;
        credIdLength.CopyTo(authData, offset);
        offset += 2;
        credentialId.CopyTo(authData, offset);
        offset += credentialId.Length;
        coseKey.CopyTo(authData, offset);

        var writer = new CborWriter();
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString(format);
        writer.WriteTextString("authData");
        writer.WriteByteString(authData);
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteEndMap();

        return writer.Encode();
    }

    public static string CreateValidSignature(ECDsa ecdsa, byte[] authenticatorData, byte[] clientDataJson,
        DSASignatureFormat format = DSASignatureFormat.Rfc3279DerSequence)
    {
        ArgumentNullException.ThrowIfNull(ecdsa);
        ArgumentNullException.ThrowIfNull(authenticatorData);

        var clientDataHash = SHA256.HashData(clientDataJson);
        var signedData = new byte[authenticatorData.Length + clientDataHash.Length];
        authenticatorData.CopyTo(signedData.AsSpan());
        clientDataHash.CopyTo(signedData.AsSpan(authenticatorData.Length));
        var signature = ecdsa.SignData(signedData, HashAlgorithmName.SHA256, format);
        return Base64Url.EncodeToString(signature);
    }

    internal static string CreateInvalidSignature() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));

    public static PasskeyCompleteRegistrationRequest CreateCompleteRegistrationRequest(
        Guid challengeId,
        string clientDataJson,
        byte[] attestationObject,
        byte[] credentialId,
        string name) =>
        new()
        {
            ChallengeId = challengeId,
            Id = Base64Url.EncodeToString(credentialId),
            RawId = Base64Url.EncodeToString(credentialId),
            Type = PasskeyConstants.CredentialType.PublicKey,
            Response = new AuthenticatorAttestationResponse
            {
                ClientDataJSON = clientDataJson,
                AttestationObject = Base64Url.EncodeToString(attestationObject)
            },
            Name = name
        };

    internal static PasskeyCompleteRegistrationRequest CreateCompleteRegistrationRequest(
        Guid challengeId,
        string clientDataJson,
        string origin,
        string name)
    {
        // Create a minimal attestation object (will fail validation, used for early error tests)
        var attestationObject = CreateAttestationObject(PasskeyConstants.AttestationFormat.None, new Uri(origin).Host);
        return CreateCompleteRegistrationRequest(challengeId, clientDataJson, attestationObject, [1, 2, 3, 4], name);
    }

    internal static PasskeyCompleteAuthenticationRequest CreateCompleteAuthenticationRequest(
        Guid challengeId,
        byte[] credentialId,
        string clientDataJson,
        byte[] authenticatorData,
        string signature) =>
        new()
        {
            ChallengeId = challengeId,
            Id = Base64Url.EncodeToString(credentialId),
            RawId = Base64Url.EncodeToString(credentialId),
            Type = PasskeyConstants.CredentialType.PublicKey,
            Response = new AuthenticatorAssertionResponse
            {
                ClientDataJSON = clientDataJson,
                AuthenticatorData = Base64Url.EncodeToString(authenticatorData),
                Signature = signature
            }
        };

    public static X509Certificate2 CreateAttestationCertificate(
        ECDsa key,
        string subject = "CN=Test, OU=Authenticator Attestation, O=Test Org, C=US",
        bool isCertificateAuthority = false,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null)
    {
        var certReq = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        certReq.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(isCertificateAuthority, false, 0, isCertificateAuthority));

        return certReq.CreateSelfSigned(
            notBefore ?? DateTimeOffset.UtcNow.AddDays(-1),
            notAfter ?? DateTimeOffset.UtcNow.AddDays(365));
    }

    public static byte[] CreatePackedAttestationObject(
        string relyingPartyId,
        byte[] credentialId,
        ECDsa ecdsa,
        X509Certificate2 cert,
#pragma warning disable CA1054 // This is a Base64Url-encoded clientDataJSON payload, not a URI
        string clientDataJsonBase64UrlEncoded,
#pragma warning restore CA1054
        byte flags = 0x41, // UserPresent (0x01) | AttestedCredentialData (0x40)
        Guid? aaguid = null)
    {
        ArgumentNullException.ThrowIfNull(cert);
        ArgumentNullException.ThrowIfNull(credentialId);
        ArgumentNullException.ThrowIfNull(ecdsa);
        /*
        Authenticator data layout per WebAuthn §6.1:
          [0..31]  relyingPartyIdHash — SHA-256 of the RP ID (32 bytes)
          [32]     flags               — 1 byte
          [33..36] signCount           — big-endian uint32 (4 bytes)
          --- attested credential data (present when AT flag 0x40 is set) ---
          [37..52] aaguid              — 16 bytes (zeroed for test fixtures)
          [53..54] credIdLength        — big-endian uint16
          [55..]                       credentialId ∥ coseKey
        */

        var relyingPartyIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId));
        var signCount = new byte[4]; // 4 bytes, defaults to zero

        var coseKey = CreateCoseKeyFromEcdsa(ecdsa);
        var aaguidBytes = (aaguid ?? Guid.Empty).ToByteArray(bigEndian: true);
        // credentialId length as big-endian uint16
        var credIdLength = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(credIdLength, (ushort)credentialId.Length);

        // 37 = rpIdHash (32) + flags (1) + signCount (4)
        var authData = new byte[37 + 16 + 2 + credentialId.Length + coseKey.Length];
        var offset = 0;
        relyingPartyIdHash.CopyTo(authData, offset);
        offset += 32; // relyingPartyId length
        authData[offset++] = flags;
        signCount.CopyTo(authData, offset);
        offset += 4; // signCount length
        aaguidBytes.CopyTo(authData, offset);
        offset += 16; // AAGUID length
        credIdLength.CopyTo(authData, offset);
        offset += 2; // credentialId length field
        credentialId.CopyTo(authData, offset);
        offset += credentialId.Length;
        coseKey.CopyTo(authData, offset);

        // Build the signature: sign(authData ∥ clientDataHash)
        var clientDataJsonBytes = Base64Url.DecodeFromChars(clientDataJsonBase64UrlEncoded);
        var clientDataHash = SHA256.HashData(clientDataJsonBytes);
        var signedData = new byte[authData.Length + clientDataHash.Length];
        authData.CopyTo(signedData, 0);
        clientDataHash.CopyTo(signedData, authData.Length);
        var sig = ecdsa.SignData(signedData, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        // CBOR attestation object (WebAuthn §6.5.4):
        // fmt: packed
        // authData: bytes
        // attestation statement:
        //      algorithm, signature, certificate
        var writer = new CborWriter();
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString(PasskeyConstants.AttestationFormat.Packed);
        writer.WriteTextString("authData");
        writer.WriteByteString(authData);
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(3);
        writer.WriteTextString("alg");
        writer.WriteInt32(CoseAlgorithms.Es256);
        writer.WriteTextString("sig");
        writer.WriteByteString(sig);
        writer.WriteTextString("x5c");
        writer.WriteStartArray(1); // single certificate
        writer.WriteByteString(cert.Export(X509ContentType.Cert));
        writer.WriteEndArray();
        writer.WriteEndMap();
        writer.WriteEndMap();

        return writer.Encode();
    }
}
