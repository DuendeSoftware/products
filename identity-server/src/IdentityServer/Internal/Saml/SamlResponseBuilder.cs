// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleSignin;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Internal.Saml;

internal class SamlResponseBuilder(
        IServerUrls serverUrls,
        IIssuerNameService issuerNameService,
        TimeProvider timeProvider,
        IOptions<SamlOptions> samlOptions,
        SamlClaimsService samlClaimsService,
        SamlNameIdGenerator nameIdGenerator
    )
{
    internal SamlErrorResponse BuildErrorResponse(SamlServiceProvider serviceProvider, SamlSigninRequest request,
        SamlError error)
    {
        // Use the ACS URL from the request if present and valid, otherwise fall back to SP config
        var acsUrl = request.AuthNRequest.AssertionConsumerServiceUrl
                     ?? (request.AuthNRequest.AssertionConsumerServiceIndex != null
                         ? serviceProvider.AssertionConsumerServiceUrls.ElementAtOrDefault(request.AuthNRequest
                             .AssertionConsumerServiceIndex.Value)
                         : null)
                     ?? serviceProvider.AssertionConsumerServiceUrls.First();

        return new SamlErrorResponse
        {
            ServiceProvider = serviceProvider,
            Binding = serviceProvider.AssertionConsumerServiceBinding,
            StatusCode = error.StatusCode,
            SubStatusCode = error.SubStatusCode,
            Message = error.Message,
            AssertionConsumerServiceUrl = acsUrl,
            Issuer = serverUrls.Origin, // Todo: not sure if this is a valid issuer
            InResponseTo = request.AuthNRequest.Id,
            RelayState = request.RelayState
        };
    }

    private static Conditions CreateConditions(
        SamlServiceProvider samlServiceProvider,
        DateTime issueInstant,
        TimeSpan defaultRequestMaxAge,
        TimeSpan defaultAllowedClockSkew)
    {
        var lifetime = samlServiceProvider.RequestMaxAge ?? defaultRequestMaxAge;
        var clockSkew = samlServiceProvider.ClockSkew ?? defaultAllowedClockSkew;

        return new Conditions
        {
            NotBefore = issueInstant.Subtract(clockSkew),
            NotOnOrAfter = issueInstant.Add(lifetime),
            AudienceRestrictions = [samlServiceProvider.EntityId]
        };
    }

    private static AuthnStatement CreateAuthnStatement(ClaimsPrincipal user, DateTime issueInstant, string sessionIndex)
    {
        // Determine AuthnContext based on request and user claims
        var authnContextClassRef = GetAuthnContextClassRef(user);

        return new AuthnStatement
        {
            AuthnInstant = issueInstant,
            SessionIndex = sessionIndex,
            AuthnContext = new AuthnContext { AuthnContextClassRef = authnContextClassRef }
        };
    }

    private static string GetAuthnContextClassRef(ClaimsPrincipal user)
    {
        var contextClaim = user.FindFirst(SamlConstants.ClaimTypes.AuthnContextClassRef);
        if (contextClaim == null || string.IsNullOrWhiteSpace(contextClaim.Value))
        {
            return "urn:oasis:names:tc:SAML:2.0:ac:classes:unspecified";
        }

        return contextClaim.Value.Trim();
    }

    private static string GetEmailNameId(ClaimsPrincipal user)
    {
        // Try to get email claim
        var email = user.FindFirst("email")?.Value
                    ?? user.FindFirst(ClaimTypes.Email)?.Value;

        return !string.IsNullOrEmpty(email) ? email : user.GetSubjectId();
    }

    private static Subject CreateSubject(
        SamlAuthenticationState samlAuthenticationState,
        NameIdentifier nameId,
        SamlServiceProvider serviceProvider,
        AuthNRequest? request,
        TimeSpan defaultRequestMaxAge,
        DateTime issueInstant)
    {
        var lifetime = serviceProvider.RequestMaxAge ?? defaultRequestMaxAge;
        var notOnOrAfter = issueInstant.Add(lifetime);

        return new Subject
        {
            NameId = nameId,
            SubjectConfirmations =
            [
                new()
                {
                    Method = "urn:oasis:names:tc:SAML:2.0:cm:bearer",
                    Data = new SubjectConfirmationData
                    {
                        NotOnOrAfter = notOnOrAfter,
                        Recipient = samlAuthenticationState.AssertionConsumerServiceUrl,
                        InResponseTo = request?.Id // Null for IdP-initiated
                    }
                }
            ]
        };
    }

    internal async Task<SamlResponse> BuildSuccessResponseAsync(
        ClaimsPrincipal user,
        SamlServiceProvider samlServiceProvider,
        SamlAuthenticationState samlAuthenticationState,
        string sessionIndex)
    {
        var now = timeProvider.GetUtcNow().DateTime;
        var options = samlOptions.Value;
        var nameId = nameIdGenerator.GenerateNameIdentifier(user, samlServiceProvider, samlAuthenticationState.Request);
        var attributes = await samlClaimsService.GetMappedAttributesAsync(user, samlServiceProvider);

        var acsUrl = GetAcsUrl(samlAuthenticationState.Request, samlServiceProvider);

        var issuer = await issuerNameService.GetCurrentAsync();

        return new SamlResponse
        {
            ServiceProvider = samlServiceProvider,
            InResponseTo = samlAuthenticationState.Request?.Id,
            Destination = acsUrl,
            IssueInstant = now,
            Issuer = issuer,
            Status = new Status
            {
                StatusCode = SamlStatusCodes.Success,
                NestedStatusCode = samlAuthenticationState.Request?.RequestedAuthnContext != null && !samlAuthenticationState.RequestedAuthnContextRequirementsWereMet ? SamlStatusCodes.NoAuthnContext : null,
            },
            Assertion = new Assertion
            {
                IssueInstant = now,
                Issuer = issuer,
                Subject = CreateSubject(samlAuthenticationState, nameId, samlServiceProvider,
                    samlAuthenticationState.Request, options.DefaultRequestMaxAge,
                    now),
                Conditions = CreateConditions(
                    samlServiceProvider, now,
                    options.DefaultRequestMaxAge,
                    options.DefaultClockSkew),
                AuthnStatements = [CreateAuthnStatement(user, now, sessionIndex)],
                AttributeStatements = [new AttributeStatement { Attributes = attributes.ToList() }]
            },
            RelayState = samlAuthenticationState.RelayState
        };
    }

    private static Uri GetAcsUrl(AuthNRequest? request, SamlServiceProvider samlServiceProvider)
    {
        if (request?.AssertionConsumerServiceUrl != null)
        {
            return request.AssertionConsumerServiceUrl;
        }

        if (request?.AssertionConsumerServiceIndex != null)
        {
            return samlServiceProvider.AssertionConsumerServiceUrls.ElementAt(request.AssertionConsumerServiceIndex.Value);
        }
        return samlServiceProvider.AssertionConsumerServiceUrls.FirstOrDefault()
            ?? throw new InvalidOperationException("No ACS Url defined for service provider " + samlServiceProvider.EntityId);
    }
}
