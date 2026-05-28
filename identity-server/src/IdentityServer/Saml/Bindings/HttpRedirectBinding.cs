// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Saml.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Bindings;

/// <summary>
/// Redirect binding implementation
/// </summary>
public interface IHttpRedirectBinding : IFrontChannelBinding
{
    /// <summary>
    /// Unbind from a URL.
    /// </summary>
    /// <param name="url">Url to unbind from</param>
    /// <param name="entityResolver">Resolves a SAML entity by its entity ID for signature validation</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Unbound message</returns>
    Task<InboundSaml2Message> UnBindAsync(string url, Func<string, Ct, Task<Saml2Entity?>> entityResolver, Ct ct);
}

/// <summary>
/// Saml2 Http Redirect Binding
/// </summary>
public class HttpRedirectBinding : FrontChannelBinding, IHttpRedirectBinding
{
    private readonly IdentityServerOptions _options;

    /// <summary>
    /// Constructor
    /// </summary>
    public HttpRedirectBinding(IOptions<IdentityServerOptions> options) : base(SamlConstants.Bindings.HttpRedirect)
        => _options = options.Value;

    private static readonly string[] messageNames = [SamlConstants.RequestProperties.SAMLRequest, SamlConstants.RequestProperties.SAMLResponse];

    /// <inheritdoc/>
    public override bool CanUnBind(HttpRequest httpRequest)
        => httpRequest.Method == "GET"
        && messageNames.Any(httpRequest.Query.ContainsKey);

    /// <inheritdoc/>
    public virtual async Task<InboundSaml2Message> UnBindAsync(string url, Func<string, Ct, Task<Saml2Entity?>> entityResolver, Ct ct)
    {
        var uri = new Uri(url);
        var parsed = ParseQueryString(uri.Query);

        var xml = SecureXmlParser.LoadXmlDocument(Inflate(parsed.Message), _options.Saml.MaxMessageSize).DocumentElement
            ?? throw new InvalidOperationException("The SAML message does not contain a document element");

        if (parsed.RelayState != null && Encoding.UTF8.GetByteCount(parsed.RelayState) > _options.Saml.MaxRelayStateLength)
        {
            throw new InvalidOperationException(
                $"RelayState exceeds maximum allowed size of {_options.Saml.MaxRelayStateLength} bytes.");
        }

        var trustLevel = TrustLevel.None;

        if (parsed is { Signature: not null, SigAlg: not null })
        {
            trustLevel = await ValidateRedirectSignatureAsync(xml, parsed.Signature, parsed.SigAlg, parsed.SignedContent!, entityResolver, ct);
        }
        else if (parsed.Signature != null || parsed.SigAlg != null)
        {
            throw new InvalidOperationException("Incomplete redirect binding signature parameters");
        }

        return new InboundSaml2Message
        {
            // We're not supporting destinations containing a query string.
            Destination = new UriBuilder(uri) { Query = string.Empty, Fragment = string.Empty }.Uri.ToString(),
            Name = parsed.MessageName,
            RelayState = parsed.RelayState,
            Xml = xml,
            Binding = Identifier,
            TrustLevel = trustLevel
        };
    }

    /// <inheritdoc/>
    protected override Task<InboundSaml2Message> DoUnBindAsync(HttpRequest httpRequest, Func<string, Ct, Task<Saml2Entity?>> entityResolver)
        => UnBindAsync(
            new UriBuilder(httpRequest.Scheme, httpRequest.Host.Host, httpRequest.Host.Port ?? -1, (httpRequest.PathBase + httpRequest.Path).ToString(), httpRequest.QueryString.ToString()).Uri.ToString(),
            entityResolver,
            httpRequest.HttpContext.RequestAborted);

    /// <inheritdoc/>
    protected override Task DoBindAsync(HttpResponse httpResponse, OutboundSaml2Message message)
    {
        var queryString = GetQueryString(message);

        var destinationBuilder = new UriBuilder(message.Destination);
        destinationBuilder.Query = string.IsNullOrEmpty(destinationBuilder.Query) || destinationBuilder.Query == "?"
            ? queryString.TrimStart('?')
            : destinationBuilder.Query.TrimStart('?') + "&" + queryString.TrimStart('?');

        httpResponse.Redirect(destinationBuilder.Uri.ToString());

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the query string for the message, including signing if a
    /// <see cref="OutboundSaml2Message.SigningCertificate"/> is present.
    /// </summary>
    /// <param name="message">Saml2 message</param>
    /// <returns>Query string contents as string (starting with '?')</returns>
    public static string GetQueryString(OutboundSaml2Message message)
    {
        var xmlString = message.Xml.OuterXml;
        var encoded = Deflate(xmlString);

        var queryString = $"?{message.Name}={encoded}";

        if (message.RelayState != null)
        {
            queryString += $"&RelayState={Uri.EscapeDataString(message.RelayState)}";
        }

        if (message.SigningCertificate != null)
        {
            queryString = SignQueryString(queryString, message.SigningCertificate);
        }

        return queryString;
    }

    private static string SignQueryString(string queryString, X509Certificate2 signingCertificate)
    {
        var (sigAlgUri, signFunc) = GetSigningFunction(signingCertificate);

        // Strip leading '?' for signing - per SAML HTTP-Redirect binding spec,
        // the signed content must not include the leading '?'.
        var content = queryString.TrimStart('?');
        var sigAlg = Uri.EscapeDataString(sigAlgUri);
        var toSign = $"{content}&SigAlg={sigAlg}";

        var bytesToSign = Encoding.UTF8.GetBytes(toSign);
        var signature = signFunc(bytesToSign);

        return $"?{toSign}&Signature={Uri.EscapeDataString(Convert.ToBase64String(signature))}";
    }

    private static (string AlgorithmUri, Func<byte[], byte[]> Sign) GetSigningFunction(X509Certificate2 certificate)
    {
        // Use using blocks for the type-detection keys to avoid leaking handles,
        // then acquire a fresh key inside the lambda for actual signing.
        using (var rsa = certificate.GetRSAPrivateKey())
        {
            if (rsa != null)
            {
                return (SignedXml.XmlDsigRSASHA256Url, data =>
                {
                    using var key = certificate.GetRSAPrivateKey()
                        ?? throw new InvalidOperationException("RSA private key not available for signing.");
                    return key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
                );
            }
        }

        using (var ecdsa = certificate.GetECDsaPrivateKey())
        {
            if (ecdsa != null)
            {
                return (SamlConstants.EcdsaAlgorithms.EcdsaSha256, data =>
                {
                    using var key = certificate.GetECDsaPrivateKey()
                        ?? throw new InvalidOperationException("ECDSA private key not available for signing.");
                    return key.SignData(data, HashAlgorithmName.SHA256);
                }
                );
            }
        }

        throw new InvalidOperationException(
            "The signing certificate does not contain a supported private key (RSA or ECDSA).");
    }

    private static string Deflate(string source)
    {
        using var compressed = new MemoryStream();
        using (var deflateStream = new DeflateStream(compressed, CompressionLevel.Optimal))
        {
            using var writer = new StreamWriter(deflateStream);
            writer.Write(source);
        }

        return Uri.EscapeDataString(Convert.ToBase64String(compressed.ToArray()));
    }

    private string Inflate(string source)
    {
        var compressedBytes = Convert.FromBase64String(Uri.UnescapeDataString(source));

        using var compressed = new MemoryStream(compressedBytes);
        using var deflateStream = new DeflateStream(compressed, CompressionMode.Decompress);
        using var limitedStream = new LimitedReadStream(deflateStream, _options.Saml.MaxMessageSize);
        using var reader = new StreamReader(limitedStream);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// Parsed result of a redirect query string.
    /// </summary>
    private sealed record ParsedRedirectQuery(
        string MessageName,
        string Message,
        string? RelayState,
        string? Signature,
        string? SigAlg,
        string? SignedContent);

    /// <summary>
    /// Parses the redirect binding query string parameters from the given query string.
    /// </summary>
    private static ParsedRedirectQuery ParseQueryString(string query)
    {
        var enumerable = new QueryStringEnumerable(query);

        string? messageName = null;
        string? message = null;
        string? messageRawEncoded = null;
        string? relayState = null;
        string? relayStateRawEncoded = null;
        string? signature = null;
        string? sigAlg = null;
        string? sigAlgRawEncoded = null;

        foreach (var param in enumerable)
        {
            // Simplicity is more important than performance here.
            var encodedName = param.EncodedName.ToString();

            // The standard is specific about the naming and they should never be
            // encoded. So let's find encoded only.
            if (encodedName == SamlConstants.RequestProperties.SAMLResponse
                || encodedName == SamlConstants.RequestProperties.SAMLRequest)
            {
                if (messageName != null)
                {
                    throw new InvalidOperationException($"Duplicate message parameters found: {messageName}, {encodedName}");
                }

                messageName = encodedName;
                message = param.DecodeValue().ToString();
                messageRawEncoded = param.EncodedValue.ToString();
            }

            if (encodedName == SamlConstants.RequestProperties.RelayState)
            {
                if (relayState != null)
                {
                    throw new InvalidOperationException("Duplicate RelayState parameters found");
                }
                relayState = param.DecodeValue().ToString();
                relayStateRawEncoded = param.EncodedValue.ToString();
            }

            if (encodedName == SamlConstants.RequestProperties.SigAlg)
            {
                if (sigAlg != null)
                {
                    throw new InvalidOperationException("Duplicate SigAlg parameters found");
                }
                sigAlg = param.DecodeValue().ToString();
                sigAlgRawEncoded = param.EncodedValue.ToString();
            }

            if (encodedName == SamlConstants.RequestProperties.Signature)
            {
                if (signature != null)
                {
                    throw new InvalidOperationException("Duplicate Signature parameters found");
                }
                signature = param.DecodeValue().ToString();
            }
        }

        if (messageName == null || message == null)
        {
            throw new InvalidOperationException("SAMLResponse or SAMLRequest parameter not found");
        }

        // Treat empty values as absent
        if (string.IsNullOrEmpty(signature))
        {
            signature = null;
        }
        if (string.IsNullOrEmpty(sigAlg))
        {
            sigAlg = null;
        }

        string? signedContent = null;
        if (signature != null && sigAlg != null && sigAlgRawEncoded != null)
        {
            var sb = new StringBuilder();
            sb.Append(messageName).Append('=').Append(messageRawEncoded);
            if (relayStateRawEncoded != null)
            {
                sb.Append('&').Append(SamlConstants.RequestProperties.RelayState)
                  .Append('=').Append(relayStateRawEncoded);
            }
            sb.Append('&').Append(SamlConstants.RequestProperties.SigAlg)
              .Append('=').Append(sigAlgRawEncoded);
            signedContent = sb.ToString();
        }

        return new ParsedRedirectQuery(messageName, message, relayState, signature, sigAlg, signedContent);
    }

    private static async Task<TrustLevel> ValidateRedirectSignatureAsync(
        System.Xml.XmlElement xml,
        string signature,
        string sigAlg,
        string signedContent,
        Func<string, Ct, Task<Saml2Entity?>> entityResolver,
        Ct ct)
    {
        var firstChild = xml.ChildNodes.OfType<System.Xml.XmlElement>().FirstOrDefault();
        var issuer = firstChild is { LocalName: SamlConstants.Elements.Issuer, NamespaceURI: SamlConstants.Namespaces.Assertion }
            ? firstChild.InnerText
            : null;
        if (string.IsNullOrEmpty(issuer))
        {
            return TrustLevel.None;
        }

        var entity = await entityResolver(issuer, ct);
        if (entity?.SigningKeys is not { } keys)
        {
            return TrustLevel.None;
        }

        // Check against the entity's allowed algorithms (per-SP or global default)
        if (!entity.AllowedAlgorithms.Contains(sigAlg, StringComparer.Ordinal))
        {
            return TrustLevel.None;
        }

        var certificates = keys.Select(k => k.Certificate).OfType<X509Certificate2>().ToList();
        if (certificates.Count == 0)
        {
            return TrustLevel.None;
        }

        var contentBytes = Encoding.UTF8.GetBytes(signedContent);
        byte[] signatureBytes;

        try
        {
            signatureBytes = Convert.FromBase64String(signature);
        }
        catch (FormatException)
        {
            return TrustLevel.None;
        }

        return ValidateSignature(contentBytes, signatureBytes, sigAlg, certificates)
            ? TrustLevel.ConfiguredKey | TrustLevel.HasSignature
            : TrustLevel.None;
    }

    private enum SignatureKeyType { Rsa, Ecdsa }

    ///<summary>
    /// Maps signature algorithm URIs to their hash algorithm and key type for verification.
    /// This is a technical codec — policy decisions about which algorithms are allowed
    /// are handled per-SP via <see cref="Saml2Entity.AllowedAlgorithms"/>.
    /// </summary>
    private static readonly Dictionary<string, (HashAlgorithmName Hash, SignatureKeyType KeyType)> SupportedSignatureAlgorithms = new(StringComparer.Ordinal)
    {
        [SignedXml.XmlDsigRSASHA256Url] = (HashAlgorithmName.SHA256, SignatureKeyType.Rsa),
        [SignedXml.XmlDsigRSASHA384Url] = (HashAlgorithmName.SHA384, SignatureKeyType.Rsa),
        [SignedXml.XmlDsigRSASHA512Url] = (HashAlgorithmName.SHA512, SignatureKeyType.Rsa),
        // INFO: legacy algorithms are allowed here, but MUST be enabled on the SP and are not by default
        [SignedXml.XmlDsigRSASHA1Url] = (HashAlgorithmName.SHA1, SignatureKeyType.Rsa),
        ["http://www.w3.org/2001/04/xmldsig-more#rsa-md5"] = (HashAlgorithmName.MD5, SignatureKeyType.Rsa),
        [SamlConstants.EcdsaAlgorithms.EcdsaSha256] = (HashAlgorithmName.SHA256, SignatureKeyType.Ecdsa),
        [SamlConstants.EcdsaAlgorithms.EcdsaSha384] = (HashAlgorithmName.SHA384, SignatureKeyType.Ecdsa),
        [SamlConstants.EcdsaAlgorithms.EcdsaSha512] = (HashAlgorithmName.SHA512, SignatureKeyType.Ecdsa),
    };

    private static bool ValidateSignature(
        byte[] signedContent,
        byte[] signatureBytes,
        string sigAlg,
        ICollection<X509Certificate2> certificates)
    {
        if (!SupportedSignatureAlgorithms.TryGetValue(sigAlg, out var algorithm))
        {
            return false;
        }

        // Strictly partition by key type — sigAlg is attacker-controlled,
        // so we must never cross key types (algorithm confusion prevention).
        return algorithm.KeyType switch
        {
            SignatureKeyType.Rsa => VerifyWithKey(certificates, signedContent, signatureBytes, algorithm.Hash,
                static cert => cert.GetRSAPublicKey(),
                static (key, data, sig, hash) => key.VerifyData(data, sig, hash, RSASignaturePadding.Pkcs1)),
            SignatureKeyType.Ecdsa => VerifyWithKey(certificates, signedContent, signatureBytes, algorithm.Hash,
                static cert => cert.GetECDsaPublicKey(),
                static (key, data, sig, hash) => key.VerifyData(data, sig, hash)),
            _ => false
        };
    }

    private static bool VerifyWithKey<TKey>(
        ICollection<X509Certificate2> certificates,
        byte[] signedContent,
        byte[] signatureBytes,
        HashAlgorithmName hashAlgorithm,
        Func<X509Certificate2, TKey?> getKey,
        Func<TKey, byte[], byte[], HashAlgorithmName, bool> verify)
        where TKey : AsymmetricAlgorithm
    {
        foreach (var cert in certificates)
        {
            using var key = getKey(cert);
            if (key == null)
            {
                continue;
            }

            if (verify(key, signedContent, signatureBytes, hashAlgorithm))
            {
                return true;
            }
        }

        return false;
    }
}
