// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Parses a return URL using all registered URL parsers
/// </summary>
public class ReturnUrlParser
{
    private readonly IEnumerable<IReturnUrlParser> _parsers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnUrlParser"/> class.
    /// </summary>
    /// <param name="parsers">The parsers.</param>
    public ReturnUrlParser(IEnumerable<IReturnUrlParser> parsers) => _parsers = parsers;

    /// <summary>
    /// Parses the return URL.
    /// </summary>
    /// <param name="returnUrl">The return URL.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public virtual async Task<AuthorizationRequest> ParseAsync(string returnUrl, CT ct)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("ReturnUrlParser.Parse");

        foreach (var parser in _parsers)
        {
            var result = await parser.ParseAsync(returnUrl, ct);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether a return URL is valid.
    /// </summary>
    /// <param name="returnUrl">The return URL.</param>
    /// <returns>
    ///   <c>true</c> if the return URL is valid; otherwise, <c>false</c>.
    /// </returns>
    public virtual bool IsValidReturnUrl(string returnUrl)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("ReturnUrlParser.IsValidReturnUrl");

        foreach (var parser in _parsers)
        {
            if (parser.IsValidReturnUrl(returnUrl))
            {
                return true;
            }
        }

        return false;
    }
}
