// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Validation;

public class AttestationSecretParser : ISecretParser
{
    private readonly IdentityServerOptions _options;
    private readonly ILogger<AttestationSecretParser> _logger;

    public AttestationSecretParser(IOptions<IdentityServerOptions> options, ILogger<AttestationSecretParser> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns the authentication method name that this parser implements
    /// </summary>
    /// <value>
    /// The authentication method.
    /// </value>
    public string AuthenticationMethod => "attestation";

    /// <summary>
    /// Tries to find a secret on the context that can be used for authentication
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>
    /// A parsed secret
    /// </returns>
    public Task<ParsedSecret> ParseAsync(HttpContext context)
    {
        if (!TryParseJwtsFromHeaders(context, out var jwtHeaders) &&
            !TryParseJwtsFromConcatenatedSerializationFormat(context, out jwtHeaders))
        {
            _logger.LogDebug("Failed to parse JWTs from headers");
            return Task.FromResult<ParsedSecret>(null);
        }

        if (!TryParseClientId(context, out var clientId))
        {
            _logger.LogDebug("Failed to parse client id");
            return Task.FromResult<ParsedSecret>(null);
        }

        var parsedSecret = new ParsedSecret
        {
            Id = clientId,
            Type = IdentityServerConstants.ParsedSecretTypes.AttestationBased,
            Credential = new AttestationSecretValidationContext
            {
                ClientId = clientId,
                ClientAttestationJwt = jwtHeaders.Item1,
                ClientAttestationPopJwt = jwtHeaders.Item2
            }
        };

        return Task.FromResult(parsedSecret);
    }

    private bool TryParseJwtsFromHeaders(HttpContext context, out (string, string) tokens)
    {
        tokens = (null, null);
        if (!context.Request.Headers.TryGetValue("OAuth-Client-Attestation", out var clientAttestationJwt))
        {
            _logger.LogDebug("Missing OAuth-Client-Attestation header");
            return false;
        }

        if (clientAttestationJwt.Count != 1)
        {
            _logger.LogDebug("Not exactly one value for OAuth-Client-Attestation header");
            return false;
        }

        if (!context.Request.Headers.TryGetValue("OAuth-Client-Attestation-PoP", out var clientAttestationPopJwt))
        {
            _logger.LogDebug("Missing OAuth-Client-Attestation-PoP header");
            return false;
        }

        if (clientAttestationPopJwt.Count != 1)
        {
            _logger.LogDebug("Not exactly one value for OAuth-Client-Attestation-PoP header");
            return false;
        }

        tokens = (clientAttestationJwt, clientAttestationPopJwt);

        return true;
    }

    private bool TryParseJwtsFromConcatenatedSerializationFormat(HttpContext context, out (string, string) tokens)
    {
        tokens = (null, null);
        if (!context.Request.HasApplicationFormContentType())
        {
            _logger.LogDebug("Content type is not a form");
            return false;
        }

        var concatenatedSerializationFormat = context.Request.Form["OAuth-Client-Attestation"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(concatenatedSerializationFormat))
        {
            _logger.LogDebug("Missing OAuth-Client-Attestation in body");
            return false;
        }

        var parts = concatenatedSerializationFormat.Split('~');
        if (parts.Length != 2)
        {
            _logger.LogDebug("Invalid concatenated serialization format");
            return false;
        }

        tokens = (parts[0], parts[1]);
        return true;
    }

    private bool TryParseClientId(HttpContext context, out string clientId)
    {
        if (!context.Request.Query.TryGetValue("client_id", out var clientIdFromRequest) &&
            !(context.Request.HasApplicationFormContentType() && context.Request.Form.TryGetValue("client_id", out clientIdFromRequest)))
        {
            _logger.LogDebug("Client Id is missing from request");
            clientId = null;

            return false;
        }

        if (clientIdFromRequest.Count != 1)
        {
            _logger.LogDebug("Not exactly one value for client_id");
            clientId = null;

            return false;
        }

        clientId = clientIdFromRequest;
        if (clientId.Length > _options.InputLengthRestrictions.ClientId)
        {
            _logger.LogDebug("Client Id exceeds maximum length");
            clientId = null;

            return false;
        }

        return true;
    }
}
