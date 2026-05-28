// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Endpoints.Results;

/// <summary>
/// Result when Saml2 single sign on resulted in login being required.
/// </summary>
/// <param name="validatedAuthnRequest">AuthnRequest validation context</param>
/// <param name="redirectUrl">Url to redirect to</param>
/// <param name="returnUrlParameter">Name of returnUrl query string param</param>
public class Saml2LoginPageResult(
    ValidatedAuthnRequest validatedAuthnRequest,
    string? redirectUrl,
    string? returnUrlParameter)
    : EndpointResult<Saml2LoginPageResult>
{
    /// <summary>
    /// The validated SAML authentication request.
    /// </summary>
    public ValidatedAuthnRequest Request { get; } = validatedAuthnRequest ?? throw new ArgumentNullException(nameof(validatedAuthnRequest));

    /// <summary>
    /// Url to redirect to (i.e. the login endpoint).
    /// </summary>
    public string RedirectUrl { get; } = redirectUrl ?? throw new ArgumentNullException(nameof(redirectUrl));

    /// <summary>
    /// Name of the returnUrl parameter to attach to redirect.
    /// </summary>
    public string ReturnUrlParameterName { get; } = returnUrlParameter ?? throw new ArgumentNullException(nameof(returnUrlParameter));
}

/// <summary>
/// Response writer for redirecting to the login page, persisting SAML state
/// and passing the state identifier through the return URL query string.
/// </summary>
internal sealed class Saml2LoginPageResultHttpWriter(
    ISamlSigninStateStore stateStore,
    IServerUrls serverUrls,
    TimeProvider timeProvider,
    IOptions<IdentityServerOptions> identityServerOptions)
    : IHttpResponseWriter<Saml2LoginPageResult>
{
    private readonly SamlEndpointOptions _endpoints = identityServerOptions.Value.Saml.Endpoints;

    /// <inheritdoc/>
    public async Task WriteHttpResponse(Saml2LoginPageResult result, HttpContext context)
    {
        var request = result.Request;

        var state = new SamlAuthenticationState
        {
            AuthnRequestData = request.AuthnRequest is { } authn ? new StoredAuthnRequestData
            {
                RequestId = authn.Id,
                ForceAuthn = authn.ForceAuthn,
                IsPassive = authn.IsPassive,
                NameIdPolicyFormat = authn.NameIdPolicy?.Format,
                SubjectNameIdValue = authn.Subject?.NameId?.Value,
                // Only use as IdP hint when Scoping contains exactly one entry
                IdpHintProviderId = authn.Scoping?.IDPList?.IdpEntries.Count == 1
                    ? authn.Scoping!.IDPList!.IdpEntries.FirstOrDefault()?.ProviderId
                    : null,
                RequestedAuthnContext = authn.RequestedAuthnContext is { } rac ? new StoredRequestedAuthnContext
                {
                    Comparison = rac.Comparison,
                    AuthnContextClassRef = [.. rac.AuthnContextClassRef],
                    AuthnContextDeclRef = [.. rac.AuthnContextDeclRef]
                } : null
            } : null,
            ServiceProviderEntityId = request.Saml2Sp?.EntityId
                ?? throw new InvalidOperationException("Service provider entity ID is required."),
            RelayState = request.RelayState,
            IsIdpInitiated = request.IsIdpInitiated,
            CreatedUtc = timeProvider.GetUtcNow(),
            ExpiresAtUtc = timeProvider.GetUtcNow().Add(identityServerOptions.Value.Saml.SigninStateLifetime).UtcDateTime,
            AssertionConsumerService = request.AssertionConsumerService
                ?? throw new InvalidOperationException("Assertion consumer service is required."),
            RequestedClaimTypes = request.RequestedClaimTypes
        };

        var stateId = await stateStore.StoreSigninRequestStateAsync(state, context.RequestAborted);

        var returnUrl = serverUrls.BasePath.EnsureTrailingSlash() + _endpoints.SingleSignOnCallbackPath.TrimStart('/');

        returnUrl = returnUrl.AddQueryString(_endpoints.StateIdParameterName, stateId.ToString());

        var url = result.RedirectUrl.AddQueryString(result.ReturnUrlParameterName, returnUrl);

        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = serverUrls.GetAbsoluteUrl(url);
    }
}
