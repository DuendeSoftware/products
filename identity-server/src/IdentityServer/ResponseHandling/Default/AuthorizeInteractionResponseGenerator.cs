// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Default logic for determining if user must login or consent when making requests to the authorization endpoint.
/// </summary>
/// <seealso cref="IAuthorizeInteractionResponseGenerator" />
public class AuthorizeInteractionResponseGenerator : IAuthorizeInteractionResponseGenerator
{
    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The consent service.
    /// </summary>
    protected readonly IConsentService Consent;

    /// <summary>
    /// The profile service.
    /// </summary>
    protected readonly IProfileService Profile;

    /// <summary>
    /// The clock
    /// </summary>
    protected readonly IClock Clock;

    /// <summary>
    /// The options
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizeInteractionResponseGenerator"/> class.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="consent">The consent.</param>
    /// <param name="profile">The profile.</param>
    public AuthorizeInteractionResponseGenerator(
        IdentityServerOptions options,
        IClock clock,
        ILogger<AuthorizeInteractionResponseGenerator> logger,
        IConsentService consent,
        IProfileService profile)
    {
        Options = options;
        Clock = clock;
        Logger = logger;
        Consent = consent;
        Profile = profile;
    }

    /// <summary>
    /// Processes the interaction logic.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="consent">The consent.</param>
    /// <returns></returns>
    public virtual async Task<InteractionResponse> ProcessInteractionAsync(ValidatedAuthorizeRequest request, ConsentResponse consent = null)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("AuthorizeInteractionResponseGenerator.ProcessInteraction");
        _ = (activity?.SetTag(Tracing.Properties.ClientId, request.Client.ClientId));

        Logger.LogTrace("ProcessInteractionAsync");

        // handle the scenario where user choose to deny prior to even logging in
        if (consent != null && !consent.Granted && consent.Error.HasValue)
        {
            // special case when anonymous user has issued an error prior to authenticating
            Logger.LogInformation("Error: User consent result: {error}", consent.Error);

            var error = consent.Error switch
            {
                AuthorizationError.AccountSelectionRequired => OidcConstants.AuthorizeErrors.AccountSelectionRequired,
                AuthorizationError.ConsentRequired => OidcConstants.AuthorizeErrors.ConsentRequired,
                AuthorizationError.InteractionRequired => OidcConstants.AuthorizeErrors.InteractionRequired,
                AuthorizationError.LoginRequired => OidcConstants.AuthorizeErrors.LoginRequired,
                AuthorizationError.TemporarilyUnavailable => OidcConstants.AuthorizeErrors.TemporarilyUnavailable,
                AuthorizationError.UnmetAuthenticationRequirements => OidcConstants.AuthorizeErrors.UnmetAuthenticationRequirements,
                _ => OidcConstants.AuthorizeErrors.AccessDenied
            };

            return new InteractionResponse
            {
                Error = error,
                ErrorDescription = consent.ErrorDescription
            };
        }

        // see if create account was requested
        var result = await ProcessCreateAccountAsync(request);
        if (result.ResponseType == InteractionResponseType.None)
        {
            // see if the user needs to login
            result = await ProcessLoginAsync(request);
            if (result.ResponseType == InteractionResponseType.None)
            {
                // see if the user needs to consent
                result = await ProcessConsentAsync(request, consent);
            }
        }

        if ((result.ResponseType == InteractionResponseType.UserInteraction) && request.PromptModes.Contains(OidcConstants.PromptModes.None))
        {
            // prompt=none means do not show the UI
            Logger.LogInformation("Changing response to LoginRequired: prompt=none was requested");
            result = new InteractionResponse
            {
                Error = result.IsLogin ? OidcConstants.AuthorizeErrors.LoginRequired :
                    result.IsConsent ? OidcConstants.AuthorizeErrors.ConsentRequired :
                    OidcConstants.AuthorizeErrors.InteractionRequired
            };
        }

        return result;
    }

    /// <summary>
    /// Processes the create account logic.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    protected internal virtual Task<InteractionResponse> ProcessCreateAccountAsync(ValidatedAuthorizeRequest request)
    {
        InteractionResponse result;

        // check prompt=create here, as we don't support it with any other combo
        if (request.PromptModes.Contains(OidcConstants.PromptModes.Create))
        {
            Logger.LogInformation("Showing create account: request contains prompt=create");
            request.RemovePrompt();
            result = new InteractionResponse
            {
                IsCreateAccount = true
            };
        }
        else
        {
            result = new InteractionResponse();
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Processes the login logic.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns></returns>
    protected internal virtual async Task<InteractionResponse> ProcessLoginAsync(ValidatedAuthorizeRequest request)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("AuthorizeInteractionResponseGenerator.ProcessLogin");

        // prompt=login, prompt=select_account, and max_age=0
        //
        // These parameters are all treated the same, and force a login.
        // max_age=0 being equivalent to prompt=login is explicitly in the spec,
        // and while selecting from multiple accounts would take a significant
        // amount of work to implement, the user interaction would happen on the
        // login page.
        var showLoginBecauseOfPrompt = false; // we need this flag because we want to check for (and suppress) either OR BOTH of the prompt and max_age params
        if (request.PromptModes.Contains(OidcConstants.PromptModes.Login) ||
            request.PromptModes.Contains(OidcConstants.PromptModes.SelectAccount))
        {
            Logger.LogInformation("Showing login: request contains prompt={PromptModes}", request.PromptModes.ToSpaceSeparatedString());
            // remove prompt so when we redirect back in from login page
            // we won't think we need to force a prompt again
            request.RemovePrompt();
            showLoginBecauseOfPrompt = true;
        }
        if (request.MaxAge == 0)
        {
            Logger.LogInformation("Showing login: request contains max_age=0.");
            // remove max_age=0 so when we redirect back in from login page
            // we won't think we need to force a prompt again
            request.RemoveMaxAge();
            showLoginBecauseOfPrompt = true;
        }
        if (showLoginBecauseOfPrompt)
        {
            return new InteractionResponse { IsLogin = true };
        }

        // unauthenticated user
        var isAuthenticated = request.Subject.IsAuthenticated();

        // user de-activated
        var isActive = false;

        if (isAuthenticated)
        {
            var isActiveCtx = new IsActiveContext(request.Subject, request.Client, IdentityServerConstants.ProfileIsActiveCallers.AuthorizeEndpoint);
            await Profile.IsActiveAsync(isActiveCtx);

            isActive = isActiveCtx.IsActive;
        }

        if (!isAuthenticated || !isActive)
        {
            if (!isAuthenticated)
            {
                Logger.LogInformation("Showing login: User is not authenticated");
            }
            else if (!isActive)
            {
                Logger.LogInformation("Showing login: User is not active");
            }

            return new InteractionResponse { IsLogin = true };
        }

        // check if tenant hint matches current tenant
        if (Options.ValidateTenantOnAuthorization)
        {
            var tenant = request.GetTenant();
            if (tenant.IsPresent())
            {
                var currentTenant = request.Subject.GetTenant();
                if (tenant != currentTenant)
                {
                    Logger.LogInformation("Showing login: Current tenant ({currentTenant}) is not the requested tenant ({tenant})", currentTenant, tenant);
                    return new InteractionResponse { IsLogin = true };
                }
            }
        }

        // check current idp
        var currentIdp = request.Subject.GetIdentityProvider();

        // check if idp login hint matches current provider
        var idp = request.GetIdP();
        if (idp.IsPresent())
        {
            if (idp != currentIdp)
            {
                Logger.LogInformation("Showing login: Current IdP ({currentIdp}) is not the requested IdP ({idp})", currentIdp, idp);
                return new InteractionResponse { IsLogin = true };
            }
        }

        // check authentication freshness
        if (request.MaxAge.HasValue)
        {
            var authTime = request.Subject.GetAuthenticationTime();
            if (Clock.UtcNow.UtcDateTime > authTime.AddSeconds(request.MaxAge.Value))
            {
                Logger.LogInformation("Showing login: Requested MaxAge exceeded.");

                return new InteractionResponse { IsLogin = true };
            }
        }

        // check local idp restrictions
        if (currentIdp == IdentityServerConstants.LocalIdentityProvider)
        {
            if (!request.Client.EnableLocalLogin)
            {
                Logger.LogInformation("Showing login: User logged in locally, but client does not allow local logins");
                return new InteractionResponse { IsLogin = true };
            }
        }
        // check external idp restrictions if user not using local idp
        else if (request.Client.IdentityProviderRestrictions != null &&
                 request.Client.IdentityProviderRestrictions.Count > 0 &&
                 !request.Client.IdentityProviderRestrictions.Contains(currentIdp))
        {
            Logger.LogInformation("Showing login: User is logged in with idp: {idp}, but idp not in client restriction list.", currentIdp);
            return new InteractionResponse { IsLogin = true };
        }

        // check client's user SSO timeout
        if (request.Client.UserSsoLifetime.HasValue)
        {
            var authTimeEpoch = request.Subject.GetAuthenticationTimeEpoch();
            var nowEpoch = Clock.UtcNow.ToUnixTimeSeconds();

            var diff = nowEpoch - authTimeEpoch;
            if (diff > request.Client.UserSsoLifetime.Value)
            {
                Logger.LogInformation("Showing login: User's auth session duration: {sessionDuration} exceeds client's user SSO lifetime: {userSsoLifetime}.", diff, request.Client.UserSsoLifetime);
                return new InteractionResponse { IsLogin = true };
            }
        }

        return new InteractionResponse();
    }

    /// <summary>
    /// Processes the consent logic.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="consent">The consent.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException">Invalid PromptMode</exception>
    protected internal virtual async Task<InteractionResponse> ProcessConsentAsync(ValidatedAuthorizeRequest request, ConsentResponse consent = null)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("AuthorizeInteractionResponseGenerator.ProcessConsent");

        ArgumentNullException.ThrowIfNull(request);

        if (request.PromptModes.Any() &&
            !request.PromptModes.Contains(OidcConstants.PromptModes.None) &&
            !request.PromptModes.Contains(OidcConstants.PromptModes.Consent))
        {
            Logger.LogError("Invalid prompt mode: {promptMode}", request.PromptModes.ToSpaceSeparatedString());
            throw new ArgumentException("Invalid PromptMode");
        }

        var consentRequired = await Consent.RequiresConsentAsync(request.Subject, request.Client, request.ValidatedResources.ParsedScopes);

        if (consentRequired && request.PromptModes.Contains(OidcConstants.PromptModes.None))
        {
            Logger.LogInformation("Error: prompt=none requested, but consent is required.");

            return new InteractionResponse
            {
                Error = OidcConstants.AuthorizeErrors.ConsentRequired
            };
        }

        if (request.PromptModes.Contains(OidcConstants.PromptModes.Consent) || consentRequired)
        {
            var response = new InteractionResponse();

            // did user provide consent
            if (consent == null)
            {
                // user was not yet shown consent screen
                response.IsConsent = true;
                Logger.LogInformation("Showing consent: User has not yet consented");
            }
            else
            {
                request.WasConsentShown = true;
                Logger.LogTrace("Consent was shown to user");

                // user was shown consent -- did they say yes or no
                if (!consent.Granted)
                {
                    // no need to show consent screen again
                    // build error to return to client
                    Logger.LogInformation("Error: User consent result: {error}", consent.Error);

                    var error = consent.Error switch
                    {
                        AuthorizationError.AccountSelectionRequired => OidcConstants.AuthorizeErrors.AccountSelectionRequired,
                        AuthorizationError.ConsentRequired => OidcConstants.AuthorizeErrors.ConsentRequired,
                        AuthorizationError.InteractionRequired => OidcConstants.AuthorizeErrors.InteractionRequired,
                        AuthorizationError.LoginRequired => OidcConstants.AuthorizeErrors.LoginRequired,
                        AuthorizationError.TemporarilyUnavailable => OidcConstants.AuthorizeErrors.TemporarilyUnavailable,
                        AuthorizationError.UnmetAuthenticationRequirements => OidcConstants.AuthorizeErrors.UnmetAuthenticationRequirements,
                        _ => OidcConstants.AuthorizeErrors.AccessDenied
                    };

                    response.Error = error;
                    response.ErrorDescription = consent.ErrorDescription;
                }
                else
                {
                    // double check that required scopes are in the list of consented scopes
                    var requiredScopes = request.ValidatedResources.GetRequiredScopeValues();
                    var valid = requiredScopes.All(x => consent.ScopesValuesConsented.Contains(x));
                    if (!valid)
                    {
                        response.Error = OidcConstants.AuthorizeErrors.AccessDenied;
                        Logger.LogInformation("Error: User denied consent to required scopes");
                    }
                    else
                    {
                        // they said yes, set scopes they chose
                        request.Description = consent.Description;
                        request.ValidatedResources = request.ValidatedResources.Filter(consent.ScopesValuesConsented);
                        Logger.LogInformation("User consented to scopes: {scopes}", consent.ScopesValuesConsented);

                        if (request.Client.AllowRememberConsent)
                        {
                            // remember consent
                            var parsedScopes = Enumerable.Empty<ParsedScopeValue>();
                            if (consent.RememberConsent)
                            {
                                // remember what user actually selected
                                parsedScopes = request.ValidatedResources.ParsedScopes;
                                Logger.LogDebug("User indicated to remember consent for scopes: {scopes}", request.ValidatedResources.RawScopeValues);
                            }

                            await Consent.UpdateConsentAsync(request.Subject, request.Client, parsedScopes);
                        }
                    }
                }
            }

            return response;
        }

        return new InteractionResponse();
    }
}
