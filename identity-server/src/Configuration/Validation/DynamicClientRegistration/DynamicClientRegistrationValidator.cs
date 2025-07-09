// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration.Models;
using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Configuration.Validation.DynamicClientRegistration;

/// <inheritdoc/>
public class DynamicClientRegistrationValidator : IDynamicClientRegistrationValidator
{
    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<DynamicClientRegistrationValidator> Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicClientRegistrationValidator"/> class.
    /// </summary>
    public DynamicClientRegistrationValidator(
        ILogger<DynamicClientRegistrationValidator> logger) => Logger = logger;

    /// <inheritdoc/>
    public async Task<IDynamicClientRegistrationValidationResult> ValidateAsync(DynamicClientRegistrationContext context)
    {
        var result = await ValidateSoftwareStatementAsync(context);
        if (result is DynamicClientRegistrationError softwareStatementValidation)
        {
            return softwareStatementValidation;
        }

        result = await SetGrantTypesAsync(context);
        if (result is DynamicClientRegistrationError grantTypeValidation)
        {
            return grantTypeValidation;
        }

        result = await SetRedirectUrisAsync(context);
        if (result is DynamicClientRegistrationError redirectUrisValidation)
        {
            return redirectUrisValidation;
        }

        result = await SetScopesAsync(context);
        if (result is DynamicClientRegistrationError scopeValidation)
        {
            return scopeValidation;
        }

        result = await SetSecretsAsync(context);
        if (result is DynamicClientRegistrationError keySetValidation)
        {
            return keySetValidation;
        }

        result = await SetClientNameAsync(context);
        if (result is DynamicClientRegistrationError nameValidation)
        {
            return nameValidation;
        }

        result = await SetLogoutParametersAsync(context);
        if (result is DynamicClientRegistrationError logoutValidation)
        {
            return logoutValidation;
        }

        result = await SetMaxAgeAsync(context);
        if (result is DynamicClientRegistrationError maxAgeValidation)
        {
            return maxAgeValidation;
        }

        result = await SetUserInterfaceProperties(context);
        if (result is DynamicClientRegistrationError miscValidation)
        {
            return miscValidation;
        }

        result = await SetPublicClientProperties(context);
        if (result is DynamicClientRegistrationError publicClientValidation)
        {
            return publicClientValidation;
        }

        result = await SetAccessTokenProperties(context);
        if (result is DynamicClientRegistrationError accessTokenValidation)
        {
            return accessTokenValidation;
        }

        result = await SetIdTokenProperties(context);
        if (result is DynamicClientRegistrationError idTokenValidation)
        {
            return idTokenValidation;
        }

        result = await SetServerSideSessionProperties(context);
        if (result is DynamicClientRegistrationError serverSideSessionValidation)
        {
            return serverSideSessionValidation;
        }

        return new DynamicClientRegistrationValidatedRequest();
    }

    /// <summary>
    /// Validates requested grant types and uses them to set the allowed grant
    /// types of the client.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its allowed grant types set,
    /// the DCR request, and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetGrantTypesAsync(DynamicClientRegistrationContext context)
    {
        if (context.Request.GrantTypes.Count == 0)
        {
            return StepResult.Failure("grant type is required");
        }

        if (context.Request.GrantTypes.Contains(OidcConstants.GrantTypes.ClientCredentials))
        {
            context.Client.AllowedGrantTypes.Add(GrantType.ClientCredentials);
        }
        if (context.Request.GrantTypes.Contains(OidcConstants.GrantTypes.AuthorizationCode))
        {
            context.Client.AllowedGrantTypes.Add(GrantType.AuthorizationCode);
            if (context.Request.AuthorizationCodeLifetime.HasValue)
            {
                var lifetime = context.Request.AuthorizationCodeLifetime.Value;
                if (lifetime <= 0)
                {
                    return StepResult.Failure("The authorization code lifetime must be greater than 0 if used");
                }
                context.Client.AuthorizationCodeLifetime = lifetime;
            }
        }

        // we only support the two above grant types
        if (context.Client.AllowedGrantTypes.Count == 0)
        {
            return StepResult.Failure("unsupported grant type");
        }

        if (context.Request.GrantTypes.Contains(OidcConstants.GrantTypes.RefreshToken))
        {
            // Note that if we ever support additional grant types that allow refresh tokens, this
            // could be refactored.
            if (!context.Client.AllowedGrantTypes.Contains(GrantType.AuthorizationCode))
            {
                return StepResult.Failure("Refresh token grant requested, but no grant that supports refresh tokens was requested");
            }

            context.Client.AllowOfflineAccess = true;
            if (context.Request.RefreshTokenExpiration != null)
            {
                if (!Enum.TryParse<TokenExpiration>(context.Request.RefreshTokenExpiration, out var tokenExpiration))
                {
                    return StepResult.Failure("invalid refresh token expiration - use Absolute or Sliding (case-sensitive)");
                }
                context.Client.RefreshTokenExpiration = tokenExpiration;
            }
            if (context.Request.SlidingRefreshTokenLifetime.HasValue)
            {
                var lifetime = context.Request.SlidingRefreshTokenLifetime.Value;
                if (lifetime <= 0)
                {
                    return StepResult.Failure("The sliding refresh token lifetime must be greater than 0 if used");
                }
                context.Client.SlidingRefreshTokenLifetime = lifetime;
            }
            if (context.Request.AbsoluteRefreshTokenLifetime.HasValue)
            {
                var lifetime = context.Request.AbsoluteRefreshTokenLifetime.Value;
                if (lifetime <= 0)
                {
                    return StepResult.Failure("The absolute refresh token lifetime must be greater than 0 if used");
                }
                context.Client.AbsoluteRefreshTokenLifetime = lifetime;
            }
            if (context.Request.RefreshTokenUsage != null)
            {
                if (!Enum.TryParse<TokenUsage>(context.Request.RefreshTokenUsage, out var tokenUsage))
                {
                    return StepResult.Failure("invalid refresh token usage - use OneTimeOnly or ReUse (case-sensitive)");
                }
                context.Client.RefreshTokenUsage = tokenUsage;
            }
            if (context.Request.UpdateAccessTokenClaimsOnRefresh.HasValue)
            {
                context.Client.UpdateAccessTokenClaimsOnRefresh = context.Request.UpdateAccessTokenClaimsOnRefresh.Value;
            }
        }

        return StepResult.Success();
    }

    /// <summary>
    /// Validates requested redirect uris and uses them to set the redirect uris
    /// of the client.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its redirect uri set, the DCR
    /// request, and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetRedirectUrisAsync(DynamicClientRegistrationContext context)
    {
        if (context.Client.AllowedGrantTypes.Contains(GrantType.AuthorizationCode))
        {
            if (context.Request.RedirectUris != null)
            {
                foreach (var requestRedirectUri in context.Request.RedirectUris)
                {
                    if (requestRedirectUri.IsAbsoluteUri)
                    {
                        context.Client.RedirectUris.Add(requestRedirectUri.AbsoluteUri);
                    }
                    else
                    {
                        return StepResult.Failure("malformed redirect URI", DynamicClientRegistrationErrors.InvalidRedirectUri);
                    }
                }
            }
            else
            {
                // Note that when we implement PAR, this may no longer be an error for clients that use PAR
                return StepResult.Failure("redirect URI required for authorization_code grant type", DynamicClientRegistrationErrors.InvalidRedirectUri);
            }
        }

        if (context.Client.AllowedGrantTypes.Count == 1 &&
            context.Client.AllowedGrantTypes.FirstOrDefault(t => t.Equals(GrantType.ClientCredentials, StringComparison.Ordinal)) != null)
        {
            if (context.Request.RedirectUris?.Any() == true)
            {
                return StepResult.Failure("redirect URI not compatible with client_credentials grant type", DynamicClientRegistrationErrors.InvalidRedirectUri);
            }
        }

        return StepResult.Success();
    }

    /// <summary>
    /// Validates requested scopes and uses them to set the scopes of the
    /// client.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its scopes set, the DCR
    /// request, and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetScopesAsync(DynamicClientRegistrationContext context)
    {
        if (string.IsNullOrEmpty(context.Request.Scope))
        {
            return SetDefaultScopes(context);
        }
        else
        {
            var scopes = context.Request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (scopes.Contains("offline_access"))
            {
                scopes = scopes.Where(s => s != "offline_access").ToArray();
                Logger.LogDebug("offline_access should not be passed as a scope to dynamic client registration. Use the refresh_token grant_type instead.");
            }

            foreach (var scope in scopes)
            {
                context.Client.AllowedScopes.Add(scope);
            }
        }
        return StepResult.Success();
    }

    /// <summary>
    /// Sets scopes on the client when no scopes are requested. The default
    /// implementation sets no scopes and is intended as an extension point.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its scopes set, the DCR
    /// request, and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetDefaultScopes(DynamicClientRegistrationContext context)
    {
        Logger.LogDebug("No scopes requested for dynamic client registration, and no default scope behavior implemented. To set default scopes, extend the DynamicClientRegistrationValidator and override the SetDefaultScopes method.");
        return StepResult.Success();
    }

    /// <summary>
    /// Validates the requested jwks to set the secrets of the client.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its secrets set, the DCR
    /// request, and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetSecretsAsync(DynamicClientRegistrationContext context)
    {
        if (context.Request.JwksUri is not null && context.Request.Jwks is not null)
        {
            return StepResult.Failure("The jwks_uri and jwks parameters must not be used together");
        }

        if (context.Request.Jwks is null && context.Request.TokenEndpointAuthenticationMethod == OidcConstants.EndpointAuthenticationMethods.PrivateKeyJwt)
        {
            return StepResult.Failure("Missing jwks parameter - the private_key_jwt token_endpoint_auth_method requires the jwks parameter");

        }
        if (context.Request.Jwks is not null)
        {
            context.Request.TokenEndpointAuthenticationMethod ??= OidcConstants.EndpointAuthenticationMethods.PrivateKeyJwt;
            if (context.Request.TokenEndpointAuthenticationMethod != OidcConstants.EndpointAuthenticationMethods.PrivateKeyJwt)
            {
                return StepResult.Failure("Invalid authentication method - the jwks parameter requires the private_key_jwt token_endpoint_auth_method");
            }
        }

        if (context.Request.Jwks?.Keys is null && context.Request.RequireSignedRequestObject == true)
        {
            return StepResult.Failure("Jwks are required when the require signed request object flag is enabled");
        }

        if (context.Request.Jwks?.Keys is not null)
        {
            context.Client.RequireRequestObject = context.Request.RequireSignedRequestObject ?? false;

            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
            };

            foreach (var key in context.Request.Jwks.Keys)
            {
                var jwk = JsonSerializer.Serialize(key, jsonOptions);

                // We parse the jwk to ensure it is valid, but we ultimately
                // write the original string that was passed to us (parsing can
                // change it)
                try
                {
                    var parsedJwk = new IdentityModel.Jwk.JsonWebKey(jwk);

                    if (parsedJwk.HasPrivateKey && parsedJwk.Alg.StartsWith("HS", StringComparison.InvariantCulture))
                    {
                        return StepResult.Failure("unexpected private key in jwk");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Logger.LogError(ex, "Failed to parse jwk");
                    return StepResult.Failure("malformed jwk");
                }
                catch (JsonException ex)
                {
                    Logger.LogError(ex, "Failed to parse jwk");
                    return StepResult.Failure("malformed jwk");
                }

                context.Client.ClientSecrets.Add(new Secret
                {
                    Type = "JWK",
                    Value = jwk
                });
            }
        }
        return StepResult.Success();
    }

    /// <summary>
    /// Validates the requested client name and uses it to set the name of the
    /// client.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its name set, the DCR request,
    /// and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetClientNameAsync(DynamicClientRegistrationContext context)
    {
        context.Client.ClientName = context.Request?.ClientName;
        return StepResult.Success();
    }

    /// <summary>
    /// Validates the requested client parameters related to logout and uses
    /// them to set the corresponding properties in the client. Those parameters
    /// include the post logout redirect uris, front channel and back channel
    /// uris, and flags for the front and back channel uris indicating if they
    /// require session ids.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its logout parameters set, the
    /// DCR request, and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetLogoutParametersAsync(DynamicClientRegistrationContext context)
    {
        context.Client.PostLogoutRedirectUris = context.Request.PostLogoutRedirectUris?.Select(uri => uri.ToString()).ToList() ?? new List<string>();
        context.Client.FrontChannelLogoutUri = context.Request.FrontChannelLogoutUri?.AbsoluteUri;
        context.Client.FrontChannelLogoutSessionRequired = context.Request.FrontChannelLogoutSessionRequired ?? true;
        context.Client.BackChannelLogoutUri = context.Request.BackChannelLogoutUri?.AbsoluteUri;
        context.Client.BackChannelLogoutSessionRequired = context.Request.BackChannelLogoutSessionRequired ?? true;

        return StepResult.Success();
    }

    /// <summary>
    /// Validates the requested default max age and uses it to set the user sso
    /// lifetime of the client.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its max age set, the DCR
    /// request, and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetMaxAgeAsync(DynamicClientRegistrationContext context)
    {
        if (context.Request.DefaultMaxAge.HasValue)
        {
            if (!context.Request.GrantTypes.Contains(GrantType.AuthorizationCode))
            {
                return StepResult.Failure("default_max_age requires authorization code grant type");
            }
            var lifetime = context.Request.DefaultMaxAge;
            if (lifetime <= 0)
            {
                return StepResult.Failure("default_max_age must be greater than 0 if used");
            }
            context.Client.UserSsoLifetime = lifetime;
        }
        return StepResult.Success();
    }

    /// <summary>
    /// Validates the software statement of the request. The default
    /// implementation does nothing, and is included as an extension point.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that is being built up, the DCR request, and
    /// other contextual information.</param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> ValidateSoftwareStatementAsync(DynamicClientRegistrationContext context) => StepResult.Success();

    /// <summary>
    /// Validates the requested client parameters related to public clients and
    /// uses them to set the corresponding properties in the client. Those
    /// parameters include the require client secret flag and the allowed cors
    /// origins.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its public client properties
    /// set, the DCR request, and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetPublicClientProperties(DynamicClientRegistrationContext context)
    {
        context.Client.AllowedCorsOrigins = context.Request.AllowedCorsOrigins ?? new();
        if (context.Request.RequireClientSecret.HasValue)
        {
            context.Client.RequireClientSecret = context.Request.RequireClientSecret.Value;
        }
        return StepResult.Success();
    }

    /// <summary>
    /// Validates the requested client parameters related to access tokens and
    /// uses them to set the corresponding properties in the client. Those
    /// parameters include the allowed access token type and access token
    /// lifetime.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its access token properties
    /// set, the DCR request, and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetAccessTokenProperties(DynamicClientRegistrationContext context)
    {
        if (context.Request.AccessTokenType != null)
        {
            if (!Enum.TryParse<AccessTokenType>(context.Request.AccessTokenType, out var tokenType))
            {
                return StepResult.Failure("invalid access token type - use Jwt or Reference (case-sensitive)");
            }
            context.Client.AccessTokenType = tokenType;
        }
        if (context.Request.AccessTokenLifetime.HasValue)
        {
            var lifetime = context.Request.AccessTokenLifetime.Value;
            if (lifetime <= 0)
            {
                return StepResult.Failure("The access token lifetime must be greater than 0 if used");
            }
            context.Client.AccessTokenLifetime = lifetime;
        }
        return StepResult.Success();
    }

    /// <summary>
    /// Validates the requested client parameters related to id tokens and uses
    /// them to set the corresponding properties in the client. Those parameters
    /// include the id token lifetime and the allowed id token signing
    /// algorithms.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its id token properties set,
    /// the DCR request, and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetIdTokenProperties(DynamicClientRegistrationContext context)
    {
        if (context.Request.IdentityTokenLifetime.HasValue)
        {
            var lifetime = context.Request.IdentityTokenLifetime.Value;
            if (lifetime <= 0)
            {
                return StepResult.Failure("The identity token lifetime must be greater than 0 if used");
            }
            context.Client.IdentityTokenLifetime = lifetime;
        }
        context.Client.AllowedIdentityTokenSigningAlgorithms = context.Request.AllowedIdentityTokenSigningAlgorithms ?? new HashSet<string>();
        return StepResult.Success();
    }

    /// <summary>
    /// Validates the requested client parameters related to server side
    /// sessions and uses them to set the corresponding properties in the
    /// client. Those parameters include the coordinate lifetime with user
    /// session flag.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have its server side session
    /// properties set, the DCR request, and other contextual information.
    /// </param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetServerSideSessionProperties(DynamicClientRegistrationContext context)
    {
        if (context.Request.CoordinateLifetimeWithUserSession.HasValue)
        {
            context.Client.CoordinateLifetimeWithUserSession = context.Request.CoordinateLifetimeWithUserSession.Value;
        }
        return StepResult.Success();
    }

    /// <summary>
    /// Validates details of the request that control the user interface,
    /// including the logo uri, client uri, initiate login uri, enable local
    /// login flag, and identity provider restrictions, and uses them to set the
    /// corresponding client properties.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model that will have miscellaneous properties set,
    /// the DCR request, and other contextual information.</param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    protected virtual Task<IStepResult> SetUserInterfaceProperties(DynamicClientRegistrationContext context)
    {
        // Misc Uris
        context.Client.LogoUri = context.Request.LogoUri?.ToString();
        context.Client.InitiateLoginUri = context.Request.InitiateLoginUri?.ToString();

        // Login Providers
        if (context.Request.EnableLocalLogin.HasValue)
        {
            context.Client.EnableLocalLogin = context.Request.EnableLocalLogin.Value;
        }
        context.Client.IdentityProviderRestrictions = context.Request.IdentityProviderRestrictions ?? new();

        // Consent
        if (context.Request.RequireConsent.HasValue)
        {
            context.Client.RequireConsent = context.Request.RequireConsent.Value;
        }
        context.Client.ClientUri = context.Request.ClientUri?.AbsoluteUri;
        if (context.Request.AllowRememberConsent.HasValue)
        {
            context.Client.AllowRememberConsent = context.Request.AllowRememberConsent.Value;
        }
        if (context.Request.ConsentLifetime.HasValue)
        {
            var lifetime = context.Request.ConsentLifetime.Value;
            if (lifetime <= 0)
            {
                return StepResult.Failure("The consent lifetime must be greater than 0 if used");
            }
            context.Client.ConsentLifetime = lifetime;
        }

        return StepResult.Success();
    }
}
