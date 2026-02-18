// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

/// <summary>
/// Base class for extracting and parsing SAML protocol messages from HTTP requests.
/// Handles common logic for both HTTP-Redirect and HTTP-POST bindings.
/// </summary>
/// <typeparam name="TRequest">The type of the parsed SAML request (e.g., AuthNRequest, LogoutRequest)</typeparam>
/// <typeparam name="TResult">The type of the result containing the parsed request and metadata</typeparam>
internal abstract class SamlRequestExtractor<TRequest, TResult>
    where TRequest : ISamlRequest
    where TResult : SamlRequestBase<TRequest>
{
    private const int MaxRequestSize = 1024 * 1024; // 1MB limit

    protected abstract TRequest ParseRequest(XDocument xmlDocument);

    protected abstract TResult CreateResult(
        TRequest parsedRequest,
        XDocument requestXml,
        SamlBinding binding,
        string? relayState,
        string? signature = null,
        string? signatureAlgorithm = null,
        string? encodedSamlRequest = null);

    internal async ValueTask<TResult> ExtractAsync(HttpContext context)
    {
        var request = context.Request;

        if (request.Method == HttpMethods.Get)
        {
            return ExtractRedirectRequest(request);
        }

        if (request.Method == HttpMethods.Post)
        {
            return await ExtractPostBindingRequest(request);
        }

        throw new BadHttpRequestException($"Unsupported HTTP method '{request.Method}' for {TRequest.MessageName}");
    }

    private TResult ExtractRedirectRequest(HttpRequest request)
    {
        var encodedRequest = request.Query[SamlConstants.RequestProperties.SAMLRequest].ToString();

        if (string.IsNullOrEmpty(encodedRequest))
        {
            throw new BadHttpRequestException(
                $"Missing '{SamlConstants.RequestProperties.SAMLRequest}' query parameter in {TRequest.MessageName}");
        }

        var relayState = request.Query[SamlConstants.RequestProperties.RelayState].ToString();
        var signature = request.Query[SamlConstants.RequestProperties.Signature].ToString();
        var sigAlg = request.Query[SamlConstants.RequestProperties.SigAlg].ToString();

        // HTTP-Redirect uses deflate compression
        byte[] compressedXmlBytes;
        try
        {
            compressedXmlBytes = Convert.FromBase64String(encodedRequest);
        }
        catch (FormatException ex)
        {
            throw new BadHttpRequestException($"Invalid base64 encoding in {TRequest.MessageName}", ex);
        }
        using var compressedXmlStream = new MemoryStream(compressedXmlBytes);
        using var xmlStream = new DeflateStream(compressedXmlStream, CompressionMode.Decompress);
        using var limitedStream = new LimitedReadStream(xmlStream, MaxRequestSize);

        var (parsedRequest, xmlDocument) = LoadRequestFromStream(limitedStream);

        return CreateResult(
            parsedRequest,
            xmlDocument,
            SamlBinding.HttpRedirect,
            relayState,
            string.IsNullOrEmpty(signature) ? null : signature,
            string.IsNullOrEmpty(sigAlg) ? null : sigAlg,
            encodedRequest);
    }

    private async Task<TResult> ExtractPostBindingRequest(HttpRequest request)
    {
        if (!request.HasFormContentType)
        {
            throw new BadHttpRequestException($"POST request does not have form content type for {TRequest.MessageName}");
        }

        var form = await request.ReadFormAsync();
        var encodedRequest = form[SamlConstants.RequestProperties.SAMLRequest].ToString();

        if (string.IsNullOrEmpty(encodedRequest))
        {
            throw new BadHttpRequestException(
                $"Missing '{SamlConstants.RequestProperties.SAMLRequest}' form parameter in {TRequest.MessageName}");
        }

        var relayState = form[SamlConstants.RequestProperties.RelayState].ToString();

        // HTTP-POST has no compression
        byte[] xmlBytes;
        try
        {
            xmlBytes = Convert.FromBase64String(encodedRequest);
        }
        catch (FormatException ex)
        {
            throw new BadHttpRequestException($"Invalid base64 encoding in {TRequest.MessageName}", ex);
        }
        using var xmlStream = new MemoryStream(xmlBytes);
        await using var limitedStream = new LimitedReadStream(xmlStream, MaxRequestSize);

        var (parsedRequest, xmlDocument) = LoadRequestFromStream(limitedStream);

        return CreateResult(
            parsedRequest,
            xmlDocument,
            SamlBinding.HttpPost,
            relayState);
    }

    private (TRequest parsedRequest, XDocument xmlDocument) LoadRequestFromStream(LimitedReadStream limitedStream)
    {
        try
        {
            var xmlDocument = SecureXmlParser.LoadXDocument(limitedStream);
            var parsedRequest = ParseRequest(xmlDocument);

            return (parsedRequest, xmlDocument);
        }
        catch (FormatException ex)
        {
            throw new BadHttpRequestException($"Invalid SAMLRequest format in {TRequest.MessageName}", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new BadHttpRequestException($"Invalid SAMLRequest format in {TRequest.MessageName}", ex);
        }
        catch (XmlException ex)
        {
            throw new BadHttpRequestException($"Failed to parse SAMLRequest XML in {TRequest.MessageName}", ex);
        }
    }
}
