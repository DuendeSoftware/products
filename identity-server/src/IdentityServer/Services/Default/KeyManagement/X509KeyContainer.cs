// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Container class for X509 keys.
/// </summary>
public class X509KeyContainer : KeyContainer
{
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    /// Constructor for X509KeyContainer.
    /// </summary>
    public X509KeyContainer() => HasX509Certificate = true;

    /// <summary>
    /// Constructor for X509KeyContainer.
    /// </summary>
    public X509KeyContainer(RsaSecurityKey key, string algorithm, DateTime created, TimeSpan certAge, string issuer = "OP")
        : base(key.KeyId, algorithm, created)
    {
        HasX509Certificate = true;

        var distinguishedName = new X500DistinguishedName($"CN={issuer}");

        var request = new CertificateRequest(
          distinguishedName, key.Rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid(ServerAuthenticationOid) }, false));

        _cert = request.CreateSelfSigned(
            new DateTimeOffset(created, TimeSpan.Zero),
            new DateTimeOffset(created.Add(certAge), TimeSpan.Zero));

        CertificateRawData = Convert.ToBase64String(_cert.Export(X509ContentType.Pfx));
    }

    /// <summary>
    /// Constructor for X509KeyContainer.
    /// </summary>
    public X509KeyContainer(ECDsaSecurityKey key, string algorithm, DateTime created, TimeSpan certAge, string issuer = "OP")
        : base(key.KeyId, algorithm, created)
    {
        HasX509Certificate = true;

        var distinguishedName = new X500DistinguishedName($"CN={issuer}");

        //var ec = ECDsa.Create(key.ECDsa.par)
        var request = new CertificateRequest(distinguishedName, key.ECDsa, HashAlgorithmName.SHA256);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid(ServerAuthenticationOid) }, false));

        _cert = request.CreateSelfSigned(
            new DateTimeOffset(created, TimeSpan.Zero),
            new DateTimeOffset(created.Add(certAge), TimeSpan.Zero));

        CertificateRawData = Convert.ToBase64String(_cert.Export(X509ContentType.Pfx));
    }

    private X509Certificate2 _cert;

    /// <summary>
    /// The X509 certificate data.
    /// </summary>
    public string CertificateRawData { get; set; }

    /// <inheritdoc />
    public override AsymmetricSecurityKey ToSecurityKey()
    {
        if (_cert == null)
        {

#pragma warning disable SYSLIB0057 // Type or member is obsolete
            // TODO - Use X509CertificateLoader in a future release (when we drop NET8 support)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    _cert = new X509Certificate2(Convert.FromBase64String(CertificateRawData));
                }
                // handling this as it typically means the user profile is not loaded, and this is about the best way to detect this.
                // when the user profile is not loaded, using X509KeyStorageFlags.MachineKeySet is the only way for this to work on windows.
                // https://stackoverflow.com/questions/52750160/what-is-the-rationale-for-all-the-different-x509keystorageflags/52840537#52840537
                catch (CryptographicException ex) when (ex.HResult == unchecked((int)0x80070002)) // File not found
                {
                    _cert = new X509Certificate2(Convert.FromBase64String(CertificateRawData), (string)null, X509KeyStorageFlags.MachineKeySet);
                }
            }
            else
            {
                _cert = new X509Certificate2(Convert.FromBase64String(CertificateRawData));
            }
#pragma warning restore SYSLIB0057 // Type or member is obsolete

        }

        var key = new X509SecurityKey(_cert, Id);
        return key;
    }
}
