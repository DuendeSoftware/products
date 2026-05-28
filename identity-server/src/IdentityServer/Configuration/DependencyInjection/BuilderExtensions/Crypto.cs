// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.IdentityModel.Tokens;
using JsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder extension methods for registering crypto services
/// </summary>
public static class IdentityServerBuilderExtensionsCrypto
{
    /// <summary>
    /// Registers the provided <see cref="SigningCredentials"/> as the active signing key used by IdentityServer
    /// to sign tokens. The key must be asymmetric and use a supported signing algorithm (RS256, RS384, RS512,
    /// PS256, PS384, PS512, ES256, ES384, or ES512). The key is also registered as a validation key and
    /// will appear in the JWKS discovery document.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the signing credential to.</param>
    /// <param name="credential">The <see cref="SigningCredentials"/> containing the asymmetric key and algorithm to use for token signing.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddSigningCredential(this IIdentityServerBuilder builder, SigningCredentials credential)
    {
        if (!(credential.Key is AsymmetricSecurityKey
              || credential.Key is IdentityModel.Tokens.JsonWebKey && ((IdentityModel.Tokens.JsonWebKey)credential.Key).HasPrivateKey))
        {
            throw new InvalidOperationException("Signing key is not asymmetric");
        }

        if (!IdentityServerConstants.SupportedSigningAlgorithms.Contains(credential.Algorithm, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Signing algorithm {credential.Algorithm} is not supported.");
        }

        if (credential.Key is ECDsaSecurityKey key && !CryptoHelper.IsValidCurveForAlgorithm(key, credential.Algorithm))
        {
            throw new InvalidOperationException("Invalid curve for signing algorithm");
        }

        if (credential.Key is IdentityModel.Tokens.JsonWebKey jsonWebKey)
        {
            if (jsonWebKey.Kty == JsonWebAlgorithmsKeyTypes.EllipticCurve && !CryptoHelper.IsValidCrvValueForAlgorithm(jsonWebKey.Crv))
            {
                throw new InvalidOperationException("Invalid crv value for signing algorithm");
            }
        }

        builder.Services.AddSingleton<ISigningCredentialStore>(new InMemorySigningCredentialsStore(credential));

        var keyInfo = new SecurityKeyInfo
        {
            Key = credential.Key,
            SigningAlgorithm = credential.Algorithm
        };

        builder.Services.AddSingleton<IValidationKeysStore>(new InMemoryValidationKeysStore(new[] { keyInfo }));

        return builder;
    }

    /// <summary>
    /// Registers an X.509 certificate as the active signing credential used by IdentityServer to sign tokens.
    /// The certificate must have a private key. The signing algorithm name is appended to the key ID to allow
    /// the same certificate to be used with multiple algorithms (e.g. RS256 and PS256).
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the signing credential to.</param>
    /// <param name="certificate">The X.509 certificate with a private key to use for token signing.</param>
    /// <param name="signingAlgorithm">The signing algorithm to use (defaults to RS256).</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="certificate"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the certificate does not have a private key.</exception>
    public static IIdentityServerBuilder AddSigningCredential(this IIdentityServerBuilder builder, X509Certificate2 certificate, string signingAlgorithm = SecurityAlgorithms.RsaSha256)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("X509 certificate does not have a private key.");
        }

        // add signing algorithm name to key ID to allow using the same key for two different algorithms (e.g. RS256 and PS56);
        var key = new X509SecurityKey(certificate);
        key.KeyId += signingAlgorithm;

        var credential = new SigningCredentials(key, signingAlgorithm);
        return builder.AddSigningCredential(credential);
    }

    /// <summary>
    /// Loads an X.509 certificate from the Windows certificate store by name and registers it as the
    /// active signing credential used by IdentityServer to sign tokens.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the signing credential to.</param>
    /// <param name="name">The subject distinguished name or thumbprint of the certificate to locate in the store.</param>
    /// <param name="location">The certificate store location to search (defaults to <see cref="StoreLocation.LocalMachine"/>).</param>
    /// <param name="nameType">Specifies whether <paramref name="name"/> is a distinguished name or a thumbprint
    /// (defaults to <see cref="NameType.SubjectDistinguishedName"/>).</param>
    /// <param name="signingAlgorithm">The signing algorithm to use (defaults to RS256).</param>
    /// <exception cref="InvalidOperationException">Thrown when the certificate cannot be found in the store.</exception>
    public static IIdentityServerBuilder AddSigningCredential(
        this IIdentityServerBuilder builder,
        string name,
        StoreLocation location = StoreLocation.LocalMachine,
        NameType nameType = NameType.SubjectDistinguishedName,
        string signingAlgorithm = SecurityAlgorithms.RsaSha256)
    {
        var certificate = CryptoHelper.FindCertificate(name, location, nameType);
        if (certificate == null)
        {
            throw new InvalidOperationException($"certificate: '{name}' not found in certificate store");
        }

        return builder.AddSigningCredential(certificate, signingAlgorithm);
    }

    /// <summary>
    /// Registers the provided <see cref="SecurityKey"/> with the specified algorithm as the active signing
    /// credential used by IdentityServer to sign tokens.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the signing credential to.</param>
    /// <param name="key">The asymmetric security key to use for token signing.</param>
    /// <param name="signingAlgorithm">The signing algorithm identifier (e.g. <c>RS256</c>, <c>ES256</c>).</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddSigningCredential(this IIdentityServerBuilder builder, SecurityKey key, string signingAlgorithm)
    {
        var credential = new SigningCredentials(key, signingAlgorithm);
        return builder.AddSigningCredential(credential);
    }

    /// <summary>
    /// Registers an RSA key with the specified RSA signing algorithm as the active signing credential
    /// used by IdentityServer to sign tokens.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the signing credential to.</param>
    /// <param name="key">The RSA security key to use for token signing.</param>
    /// <param name="signingAlgorithm">The RSA-based signing algorithm to use (e.g. RS256, PS256).</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddSigningCredential(this IIdentityServerBuilder builder, RsaSecurityKey key, IdentityServerConstants.RsaSigningAlgorithm signingAlgorithm)
    {
        var credential = new SigningCredentials(key, CryptoHelper.GetRsaSigningAlgorithmValue(signingAlgorithm));
        return builder.AddSigningCredential(credential);
    }

    /// <summary>
    /// Registers an ECDsa key with the specified ECDsa signing algorithm as the active signing credential
    /// used by IdentityServer to sign tokens.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the signing credential to.</param>
    /// <param name="key">The ECDsa security key to use for token signing.</param>
    /// <param name="signingAlgorithm">The ECDsa-based signing algorithm to use (e.g. ES256, ES384, ES512).</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddSigningCredential(this IIdentityServerBuilder builder, ECDsaSecurityKey key, IdentityServerConstants.ECDsaSigningAlgorithm signingAlgorithm)
    {
        var credential = new SigningCredentials(key, CryptoHelper.GetECDsaSigningAlgorithmValue(signingAlgorithm));
        return builder.AddSigningCredential(credential);
    }

    /// <summary>
    /// Creates a temporary RSA signing key for use during development and testing. The generated key is
    /// persisted to a local <c>.jwk</c> file by default so it survives application restarts during development.
    /// <para>
    /// <strong>Not recommended for production use.</strong> Use <see cref="AddSigningCredential(IIdentityServerBuilder, SigningCredentials)"/>
    /// or automatic key management for production deployments.
    /// </para>
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the developer signing credential to.</param>
    /// <param name="persistKey">Whether to persist the generated key to disk so it survives restarts. Defaults to <see langword="true"/>.</param>
    /// <param name="filename">The file path where the key is persisted. Defaults to <c>tempkey.jwk</c> in the current directory.</param>
    /// <param name="signingAlgorithm">The RSA signing algorithm to use (defaults to RS256).</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddDeveloperSigningCredential(
        this IIdentityServerBuilder builder,
        bool persistKey = true,
        string? filename = null,
        IdentityServerConstants.RsaSigningAlgorithm signingAlgorithm = IdentityServerConstants.RsaSigningAlgorithm.RS256)
    {
        if (filename == null)
        {
            filename = Path.Combine(Directory.GetCurrentDirectory(), "tempkey.jwk");
        }

        if (File.Exists(filename))
        {
            var json = File.ReadAllText(filename);
            var jwk = new JsonWebKey(json);

            return builder.AddSigningCredential(jwk, jwk.Alg);
        }
        else
        {
            var key = CryptoHelper.CreateRsaSecurityKey();
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
            jwk.Alg = signingAlgorithm.ToString();

            if (persistKey)
            {
                File.WriteAllText(filename, System.Text.Json.JsonSerializer.Serialize(jwk));
            }

            return builder.AddSigningCredential(key, signingAlgorithm);
        }
    }

    /// <summary>
    /// Registers one or more additional keys for validating tokens. These keys are used by the internal
    /// token validator and are published in the JWKS discovery document. Use this to support key rollover
    /// by adding the previous signing key as a validation-only key while the new key is used for signing.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add validation keys to.</param>
    /// <param name="keys">One or more <see cref="SecurityKeyInfo"/> instances describing the keys and their algorithms.</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddValidationKey(this IIdentityServerBuilder builder, params SecurityKeyInfo[] keys)
    {
        builder.Services.AddSingleton<IValidationKeysStore>(new InMemoryValidationKeysStore(keys));

        return builder;
    }

    /// <summary>
    /// Registers an RSA key as an additional validation key. The key will be used by the internal token
    /// validator and published in the JWKS discovery document. Useful for key rollover scenarios.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validation key to.</param>
    /// <param name="key">The RSA security key to register for token validation.</param>
    /// <param name="signingAlgorithm">The RSA-based signing algorithm associated with this key (defaults to RS256).</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddValidationKey(
        this IIdentityServerBuilder builder,
        RsaSecurityKey key,
        IdentityServerConstants.RsaSigningAlgorithm signingAlgorithm = IdentityServerConstants.RsaSigningAlgorithm.RS256)
    {
        var keyInfo = new SecurityKeyInfo
        {
            Key = key,
            SigningAlgorithm = CryptoHelper.GetRsaSigningAlgorithmValue(signingAlgorithm)
        };

        return builder.AddValidationKey(keyInfo);
    }

    /// <summary>
    /// Registers an ECDSA key as an additional validation key. The key will be used by the internal token
    /// validator and published in the JWKS discovery document. Useful for key rollover scenarios.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validation key to.</param>
    /// <param name="key">The ECDSA security key to register for token validation.</param>
    /// <param name="signingAlgorithm">The ECDSA-based signing algorithm associated with this key (defaults to ES256).</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    public static IIdentityServerBuilder AddValidationKey(
        this IIdentityServerBuilder builder,
        ECDsaSecurityKey key,
        IdentityServerConstants.ECDsaSigningAlgorithm signingAlgorithm = IdentityServerConstants.ECDsaSigningAlgorithm.ES256)
    {
        var keyInfo = new SecurityKeyInfo
        {
            Key = key,
            SigningAlgorithm = CryptoHelper.GetECDsaSigningAlgorithmValue(signingAlgorithm)
        };

        return builder.AddValidationKey(keyInfo);
    }

    /// <summary>
    /// Registers an X.509 certificate as an additional validation key. The key will be used by the internal
    /// token validator and published in the JWKS discovery document. Useful for key rollover scenarios.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validation key to.</param>
    /// <param name="certificate">The X.509 certificate whose public key is registered for token validation.</param>
    /// <param name="signingAlgorithm">The signing algorithm associated with this certificate (defaults to RS256).</param>
    /// <returns>The <see cref="IIdentityServerBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="certificate"/> is <see langword="null"/>.</exception>
    public static IIdentityServerBuilder AddValidationKey(
        this IIdentityServerBuilder builder,
        X509Certificate2 certificate,
        string signingAlgorithm = SecurityAlgorithms.RsaSha256)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        // add signing algorithm name to key ID to allow using the same key for two different algorithms (e.g. RS256 and PS56);
        var key = new X509SecurityKey(certificate);
        key.KeyId += signingAlgorithm;

        var keyInfo = new SecurityKeyInfo
        {
            Key = key,
            SigningAlgorithm = signingAlgorithm
        };

        return builder.AddValidationKey(keyInfo);
    }

    /// <summary>
    /// Loads an X.509 certificate from the Windows certificate store by name and registers it as an
    /// additional validation key. The key will be used by the internal token validator and published
    /// in the JWKS discovery document. Useful for key rollover scenarios.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to add the validation key to.</param>
    /// <param name="name">The subject distinguished name or thumbprint of the certificate to locate in the store.</param>
    /// <param name="location">The certificate store location to search (defaults to <see cref="StoreLocation.LocalMachine"/>).</param>
    /// <param name="nameType">Specifies whether <paramref name="name"/> is a distinguished name or a thumbprint
    /// (defaults to <see cref="NameType.SubjectDistinguishedName"/>).</param>
    /// <param name="signingAlgorithm">The signing algorithm associated with this certificate (defaults to RS256).</param>
    public static IIdentityServerBuilder AddValidationKey(
        this IIdentityServerBuilder builder,
        string name,
        StoreLocation location = StoreLocation.LocalMachine,
        NameType nameType = NameType.SubjectDistinguishedName,
        string signingAlgorithm = SecurityAlgorithms.RsaSha256)
    {
        var certificate = CryptoHelper.FindCertificate(name, location, nameType);
        if (certificate == null)
        {
            throw new InvalidOperationException($"certificate: '{name}' not found in certificate store");
        }

        return builder.AddValidationKey(certificate, signingAlgorithm);
    }
}
