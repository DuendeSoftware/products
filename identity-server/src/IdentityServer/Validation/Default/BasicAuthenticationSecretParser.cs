// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Parses a Basic Authentication header
/// </summary>
public class BasicAuthenticationSecretParser : ISecretParser
{
    private readonly ILogger _logger;
    private readonly IdentityServerOptions _options;

    /// <summary>
    /// Creates the parser with a reference to identity server options
    /// </summary>
    /// <param name="options">IdentityServer options</param>
    /// <param name="logger">The logger</param>
    public BasicAuthenticationSecretParser(IdentityServerOptions options, ILogger<BasicAuthenticationSecretParser> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Returns the authentication method name that this parser implements
    /// </summary>
    /// <value>
    /// The authentication method.
    /// </value>
    public string AuthenticationMethod => OidcConstants.EndpointAuthenticationMethods.BasicAuthentication;

    /// <summary>
    /// Tries to find a secret that can be used for authentication
    /// </summary>
    /// <returns>
    /// A parsed secret
    /// </returns>
    public Task<ParsedSecret> ParseAsync(HttpContext context)
    {
        _logger.LogDebug("Start parsing Basic Authentication secret");

        var notfound = Task.FromResult<ParsedSecret>(null);
        var authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (authorizationHeader.IsMissing())
        {
            return notfound;
        }

        if (!authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return notfound;
        }

        // worst-case scenario check on the length of the Authorization header (given all the possible encoding)
        // before we start to do any real parsing or string splitting
        var schemeLength = "Basic ".Length;

        // The client and secret are first url escaped, then concatenated with a colon separator, and finally base 64 encoded
        // In the worst case, every character of the client id and secret are escaped (e.g., @ becomes %40)
        // Base 64 encoding represents 24 bits (3 bytes) with 4 encoded characters with up to 2 characters of padding
        // Thus, the worst case max length of the header is
        //   schemeLength + ((InputLengthRestrictions.ClientId + InputLengthRestrictions.ClientSecret) * 3 + 1) * 4/3 (plus 1 for colon)
        // = schemeLength + (InputLengthRestrictions.ClientId + InputLengthRestrictions.ClientSecret) * 4 + 4/3
        // We can't have 4/3 characters, so we round 4/3 up to 2 and add 2 additional bytes of padding
        // = schemeLength + (InputLengthRestrictions.ClientId + InputLengthRestrictions.ClientSecret) * 4 + 2 + 2
        // = (InputLengthRestrictions.ClientId + InputLengthRestrictions.ClientSecret) * 4 + 10

        var idAndSecret = _options.InputLengthRestrictions.ClientId + _options.InputLengthRestrictions.ClientSecret; // *3 for the URL encoding
        var authorizationHeaderHeaderMaxLength = 4 * idAndSecret + 10;

        if (authorizationHeader.Length > authorizationHeaderHeaderMaxLength)
        {
            _logger.LogError("Authorization header exceeds maximum length allowed.");
            return notfound;
        }

        var parameter = authorizationHeader.Substring(schemeLength);

        string pair;
        try
        {
            pair = Encoding.UTF8.GetString(
                Convert.FromBase64String(parameter));
        }
        catch (FormatException)
        {
            _logger.LogWarning("Malformed Basic Authentication credential.");
            return notfound;
        }
        catch (ArgumentException)
        {
            _logger.LogWarning("Malformed Basic Authentication credential.");
            return notfound;
        }

        var ix = pair.IndexOf(':', StringComparison.InvariantCulture);
        if (ix == -1)
        {
            _logger.LogWarning("Malformed Basic Authentication credential.");
            return notfound;
        }

        // RFC6749 says individual values must be application/x-www-form-urlencoded
        var clientId = UrlDecode(pair.Substring(0, ix));
        var secret = UrlDecode(pair.Substring(ix + 1));

        if (clientId.IsPresent())
        {
            if (clientId.Length > _options.InputLengthRestrictions.ClientId)
            {
                _logger.LogError("Client ID exceeds maximum length.");
                return notfound;
            }

            if (secret.IsPresent())
            {
                if (secret.Length > _options.InputLengthRestrictions.ClientSecret)
                {
                    _logger.LogError("Client secret exceeds maximum length.");
                    return notfound;
                }

                var parsedSecret = new ParsedSecret
                {
                    Id = clientId,
                    Credential = secret,
                    Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret
                };

                return Task.FromResult(parsedSecret);
            }
            else
            {
                // client secret is optional
                _logger.LogDebug("client id without secret found");

                var parsedSecret = new ParsedSecret
                {
                    Id = clientId,
                    Type = IdentityServerConstants.ParsedSecretTypes.NoSecret
                };

                return Task.FromResult(parsedSecret);
            }
        }

        _logger.LogDebug("No Basic Authentication secret found");
        return notfound;
    }

    private string UrlDecode(string value)
    {
        if (value.IsMissing())
        {
            return string.Empty;
        }

        return Uri.UnescapeDataString(value.Replace("+", "%20", StringComparison.InvariantCulture));
    }
}
