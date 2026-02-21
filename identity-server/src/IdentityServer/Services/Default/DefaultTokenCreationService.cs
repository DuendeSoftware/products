// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default token creation service
/// </summary>
public class DefaultTokenCreationService : ITokenCreationService
{
    /// <summary>
    /// The key service
    /// </summary>
    protected readonly IKeyMaterialService Keys;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    ///  The time provider
    /// </summary>
    protected readonly TimeProvider TimeProvider;

    /// <summary>
    /// The options
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTokenCreationService"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="keys">The keys.</param>
    /// <param name="options">The options.</param>
    /// <param name="logger">The logger.</param>
    public DefaultTokenCreationService(
        TimeProvider timeProvider,
        IKeyMaterialService keys,
        IdentityServerOptions options,
        ILogger<DefaultTokenCreationService> logger)
    {
        TimeProvider = timeProvider;
        Keys = keys;
        Options = options;
        Logger = logger;
    }

    /// <inheritdoc/>
    public virtual async Task<string> CreateTokenAsync(Token token, CT ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultTokenCreationService.CreateToken");

        var payload = await CreatePayloadAsync(token);
        var headerElements = await CreateHeaderElementsAsync(token);

        return await CreateJwtAsync(token, payload, headerElements, ct);
    }

    /// <summary>
    /// Creates the JWT payload
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    protected virtual Task<string> CreatePayloadAsync(Token token)
    {
        var payload = token.CreateJwtPayloadDictionary(Options, TimeProvider, Logger);
        return Task.FromResult(JsonSerializer.Serialize(payload));
    }

    /// <summary>
    /// Creates additional JWT header elements
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    protected virtual Task<Dictionary<string, object>> CreateHeaderElementsAsync(Token token)
    {
        var additionalHeaderElements = new Dictionary<string, object>();

        if (token.Type == IdentityServerConstants.TokenTypes.AccessToken)
        {
            if (Options.AccessTokenJwtType.IsPresent())
            {
                additionalHeaderElements.Add("typ", Options.AccessTokenJwtType);
            }
        }
        else if (token.Type == IdentityServerConstants.TokenTypes.LogoutToken)
        {
            if (Options.LogoutTokenJwtType.IsPresent())
            {
                additionalHeaderElements.Add("typ", Options.LogoutTokenJwtType);
            }
        }
        else if (token.Type == JwtClaimTypes.JwtTypes.IntrospectionJwtResponse)
        {
            additionalHeaderElements.Add("typ", "token-introspection+jwt");
        }

        return Task.FromResult(additionalHeaderElements);
    }

    /// <summary>
    /// Creates JWT token
    /// </summary>
    /// <param name="token"></param>
    /// <param name="payload"></param>
    /// <param name="headerElements"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected virtual async Task<string> CreateJwtAsync(Token token, string payload,
        Dictionary<string, object> headerElements, CT ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultTokenCreationService.CreateJwt");

        var credential = await Keys.GetSigningCredentialsAsync(token.AllowedSigningAlgorithms, ct);

        if (credential == null)
        {
            throw new InvalidOperationException("No signing credential is configured. Can't create JWT token");
        }

        var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };
        return handler.CreateToken(payload, credential, headerElements);
    }
}
