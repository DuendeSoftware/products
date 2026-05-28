// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.ResponseHandling;

/// <summary>
/// Response Generator for Saml2 Single Sign On.
/// </summary>
/// <param name="issuerNameService">Issuer name service for Saml2</param>
/// <param name="timeProvider">Clock</param>
/// <param name="samlXmlWriter">Xml Writer/serializer</param>
/// <param name="profileService">Profile Service</param>
/// <param name="samlSigningService">Signing service for SAML responses</param>
/// <param name="identityServerOptions">IdentityServer configuration options</param>
/// <param name="nameIdGenerator">NameID generator for subject identification</param>
public class Saml2SSoResponseGenerator(
    ISaml2IssuerNameService issuerNameService,
    TimeProvider timeProvider,
    ISamlXmlWriter samlXmlWriter,
    IProfileService profileService,
    ISamlSigningService samlSigningService,
    IOptions<IdentityServerOptions> identityServerOptions,
    ISamlNameIdGenerator nameIdGenerator)
    : ISaml2SsoResponseGenerator
{
    private readonly SamlOptions _samlOptions = identityServerOptions.Value.Saml;
    /// <inheritdoc/>
    public async Task<Saml2FrontChannelResult> CreateResponse(ValidatedAuthnRequest validatedAuthnRequest, Ct ct)
    {
        var nameIdResult = await CreateSubjectNameIdAsync(validatedAuthnRequest, ct);
        if (nameIdResult.IsError)
        {
            // NameID generation failures route to the error page rather than producing a
            // SAML error response. This prevents leaking implementation details to the SP.
            return new Saml2FrontChannelResult { Error = nameIdResult.Error!.Message };
        }

        var nameId = nameIdResult.NameId!;
        var response = await CreateSaml2ResponseAsync(validatedAuthnRequest, nameId, ct);
        var signingBehavior = validatedAuthnRequest.Saml2Sp?.SigningBehavior
            ?? _samlOptions.DefaultSigningBehavior;
        var signAssertions = signingBehavior.HasFlag(SamlSigningBehavior.SignAssertion);
        var signResponse = signingBehavior.HasFlag(SamlSigningBehavior.SignResponse);

        var signingCertificate = signAssertions || signResponse
            ? await samlSigningService.GetSigningCertificateAsync(ct)
            : null;

        if ((signAssertions || signResponse) && signingCertificate == null)
        {
            throw new InvalidOperationException(
                "Signing was requested but no signing certificate is available.");
        }

        if (signAssertions)
        {
            samlXmlWriter.AssertionSigningCertificate = signingCertificate;
        }

        var xml = samlXmlWriter.Write(response);
        var responseBinding = GetSupportedResponseBindingUrn(validatedAuthnRequest.AssertionConsumerService!.Binding);

        return new()
        {
            Message = new()
            {
                Destination = response.Destination!,
                Name = SamlConstants.RequestProperties.SAMLResponse,
                RelayState = validatedAuthnRequest.RelayState,
                Xml = xml.DocumentElement!,
                SigningCertificate = signResponse ? signingCertificate : null,
                Binding = responseBinding
            },
            GeneratedNameId = nameId
        };
    }

    /// <inheritdoc/>
    public async Task<Saml2FrontChannelResult> CreateErrorResponse(
        ValidatedAuthnRequest validatedAuthnRequest,
        Saml2InteractionResponse interactionResponse,
        Ct ct)
    {
        if (!interactionResponse.IsError || interactionResponse.StatusCode == null)
        {
            throw new ArgumentException("Cannot create an error response from a non-error interaction response.", nameof(interactionResponse));
        }

        if (!IsSafeError(validatedAuthnRequest, interactionResponse))
        {
            return new()
            {
                Error = interactionResponse.Message,
                SpEntityId = validatedAuthnRequest.Saml2Sp?.EntityId
            };
        }

        var issuer = await issuerNameService.GetCurrentAsync(ct);
        var destination = validatedAuthnRequest.AssertionConsumerService!.Location;

        var statusCode = new StatusCode
        {
            Value = interactionResponse.StatusCode!
        };

        if (interactionResponse.SubStatusCode != null)
        {
            statusCode.NestedStatusCode = new StatusCode
            {
                Value = interactionResponse.SubStatusCode
            };
        }

        var response = new Response
        {
            Destination = destination,
            Issuer = issuer,
            IssueInstant = timeProvider.GetUtcNow().UtcDateTime,
            InResponseTo = validatedAuthnRequest.RequestId,
            Status = new SamlStatus
            {
                StatusCode = statusCode
            }
        };

        var signingBehavior = validatedAuthnRequest.Saml2Sp?.SigningBehavior
            ?? _samlOptions.DefaultSigningBehavior;
        var signResponse = signingBehavior.HasFlag(SamlSigningBehavior.SignResponse);

        var signingCertificate = signResponse
            ? await samlSigningService.GetSigningCertificateAsync(ct)
            : null;

        if (signResponse && signingCertificate == null)
        {
            throw new InvalidOperationException(
                "Signing was requested but no signing certificate is available.");
        }

        // Do not set samlXmlWriter.AssertionSigningCertificate — error responses have no assertions
        var xml = samlXmlWriter.Write(response);
        var responseBinding = GetSupportedResponseBindingUrn(validatedAuthnRequest.AssertionConsumerService!.Binding);

        return new()
        {
            Message = new()
            {
                Destination = destination,
                Name = SamlConstants.RequestProperties.SAMLResponse,
                RelayState = validatedAuthnRequest.RelayState,
                Xml = xml.DocumentElement!,
                SigningCertificate = signResponse ? signingCertificate : null,
                Binding = responseBinding
            }
        };
    }

    /// <summary>
    /// Ensures the resolved ACS binding is one this IdP supports for response delivery.
    /// </summary>
    /// <param name="binding">The binding from the ACS endpoint.</param>
    /// <returns>The binding URN string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the binding is not supported.</exception>
    private static string GetSupportedResponseBindingUrn(SamlBinding binding)
    {
        if (binding != SamlBinding.HttpPost)
        {
            throw new InvalidOperationException(
                $"The ACS binding '{binding}' is not supported for SAML response delivery. " +
                "Only HTTP-POST is supported.");
        }

        return binding.ToUrn();
    }

    /// <summary>
    /// Determines whether an interaction error is safe to send back to the SP as a SAML error response.
    /// When <see langword="true"/>, a SAML error <c>&lt;Response&gt;</c> is sent to the SP's ACS URL.
    /// When <see langword="false"/>, the user is redirected to the error page instead.
    /// </summary>
    /// <remarks>
    /// The default implementation returns <see langword="true"/> for all interaction errors, since
    /// they occur after full AuthnRequest validation — the SP and ACS URL are always verified at this point.
    /// Override to suppress specific errors from being sent back to the SP.
    /// </remarks>
    /// <param name="validatedAuthnRequest">The validated AuthnRequest</param>
    /// <param name="interactionResponse">The interaction error response</param>
    /// <returns><see langword="true"/> if the error should be sent to the SP; <see langword="false"/> to show the error page</returns>
    protected virtual bool IsSafeError(ValidatedAuthnRequest validatedAuthnRequest, Saml2InteractionResponse interactionResponse)
        => true;

    /// <summary>
    /// Create the Saml2 response.
    /// </summary>
    /// <param name="validatedAuthnRequest">AuthnRequest validation context</param>
    /// <param name="nameId">The generated NameID for the subject</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>The Saml2 Response object</returns>
    protected virtual async Task<Response> CreateSaml2ResponseAsync(ValidatedAuthnRequest validatedAuthnRequest, NameId nameId, Ct ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var issuer = await issuerNameService.GetCurrentAsync(ct);
        var destination = validatedAuthnRequest.AssertionConsumerService!.Location;

        return new Response
        {
            Destination = destination,
            Issuer = issuer,
            Status = new()
            {
                StatusCode = new()
                {
                    Value = SamlStatusCodes.Success
                }
            },
            IssueInstant = now,
            InResponseTo = validatedAuthnRequest.RequestId,
            Assertions =
            {
                await CreateAssertionAsync(validatedAuthnRequest, nameId, issuer, destination, now, ct)
            }
        };
    }

    /// <summary>
    /// Create the Assertion
    /// </summary>
    /// <param name="validatedAuthnRequest">AuthnRequest validation context</param>
    /// <param name="nameId">The generated NameID for the subject</param>
    /// <param name="issuer">The issuer to use</param>
    /// <param name="destination">Destination URL</param>
    /// <param name="now">Current UTC timestamp</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Assertion</returns>
    protected virtual async Task<Saml.Assertion> CreateAssertionAsync(ValidatedAuthnRequest validatedAuthnRequest, NameId nameId, string issuer, string destination, DateTime now, Ct ct)
    {
        var lifetime = validatedAuthnRequest.Saml2Sp?.AssertionLifetime ?? _samlOptions.DefaultAssertionLifetime;

        return new()
        {
            Issuer = issuer,
            IssueInstant = now,
            Subject = CreateSubject(validatedAuthnRequest, nameId, destination, now, lifetime),
            Conditions = CreateConditions(validatedAuthnRequest, now, lifetime),
            AuthnStatement = CreateAuthnStatement(validatedAuthnRequest.Subject!, now, validatedAuthnRequest.Saml2Sp!, validatedAuthnRequest.SessionIndex),
            Attributes = [.. await CreateAttributesAsync(validatedAuthnRequest, ct)]
        };
    }

    /// <summary>
    /// Create the AuthnStatement. Resolves the <c>AuthnContextClassRef</c> by looking up the
    /// user's <c>acr</c> claim, then <c>amr</c> claim(s), against the configured mappings.
    /// Falls back to <see cref="SamlConstants.AuthnContextClasses.Unspecified"/> if no match is found.
    /// </summary>
    /// <param name="subject">The authenticated user</param>
    /// <param name="now">Current UTC timestamp</param>
    /// <param name="saml2Sp">The service provider configuration</param>
    /// <param name="sessionIndex">The session index to include in the statement</param>
    /// <returns>AuthnStatement</returns>
    protected virtual Saml.AuthnStatement CreateAuthnStatement(ClaimsPrincipal subject, DateTime now, SamlServiceProvider saml2Sp, string? sessionIndex)
    {
        var mappings = saml2Sp.AuthnContextMappings.Count > 0
            ? saml2Sp.AuthnContextMappings
            : _samlOptions.DefaultAuthnContextMappings;

        return new()
        {
            AuthnInstant = now,
            SessionIndex = sessionIndex,
            AuthnContext = new()
            {
                AuthnContextClassRef = ResolveAuthnContextClassRef(subject, mappings)
            }
        };
    }

    /// <summary>
    /// Create the Conditions
    /// </summary>
    /// <param name="validatedAuthnRequest">AuthnRequest validation context</param>
    /// <param name="now">Current UTC timestamp</param>
    /// <param name="lifetime">Assertion lifetime</param>
    /// <returns>Conditions</returns>
    protected virtual Saml.Conditions CreateConditions(ValidatedAuthnRequest validatedAuthnRequest, DateTime now, TimeSpan lifetime)
        => new()
        {
            NotBefore = now,
            NotOnOrAfter = now.Add(lifetime),
            AudienceRestrictions =
            {
                new()
                {
                    Audiences = { validatedAuthnRequest.Saml2Sp!.EntityId }
                }
            }
        };

    /// <summary>
    /// Generate the NameID for the subject using the configured <see cref="ISamlNameIdGenerator"/>.
    /// </summary>
    /// <param name="validatedAuthnRequest">AuthnRequest validation context</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>The NameID generation result</returns>
    protected virtual Task<NameIdGenerationResult> CreateSubjectNameIdAsync(ValidatedAuthnRequest validatedAuthnRequest, Ct ct)
    {
        var format = validatedAuthnRequest.NameIdPolicyFormat
                     ?? validatedAuthnRequest.Saml2Sp?.DefaultNameIdFormat
                     ?? SamlConstants.NameIdentifierFormats.Unspecified;

        var context = new NameIdGenerationContext
        {
            Subject = validatedAuthnRequest.Subject!,
            ServiceProvider = validatedAuthnRequest.Saml2Sp!,
            ResolvedFormat = format
        };

        return nameIdGenerator.GenerateAsync(context, ct);
    }

    /// <summary>
    /// Create the Subject
    /// </summary>
    /// <param name="validatedAuthnRequest">AuthnRequest validation context</param>
    /// <param name="nameId">The generated NameID</param>
    /// <param name="destination">Destination URL</param>
    /// <param name="now">Current UTC timestamp</param>
    /// <param name="lifetime">Assertion lifetime</param>
    /// <returns>Subject</returns>
    protected virtual Subject CreateSubject(ValidatedAuthnRequest validatedAuthnRequest, NameId nameId, string destination, DateTime now, TimeSpan lifetime)
        => new()
        {
            NameId = nameId,
            SubjectConfirmation = new()
            {
                Method = SamlConstants.SubjectConfirmationMethods.Bearer,
                SubjectConfirmationData = new()
                {
                    NotOnOrAfter = now.Add(lifetime),
                    Recipient = destination,
                    InResponseTo = validatedAuthnRequest.RequestId
                }
            }
        };

    /// <summary>
    /// Create Attributes
    /// </summary>
    /// <param name="validatedAuthnRequest">AuthnRequest validation context</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Attributes</returns>
    protected virtual async Task<IList<SamlAttribute>> CreateAttributesAsync(ValidatedAuthnRequest validatedAuthnRequest, Ct ct)
    {
        var sp = validatedAuthnRequest.Saml2Sp!;

        ProfileDataRequestContext profileRequest = new()
        {
            Caller = SamlConstants.SsoResponseProfileCaller,
            Application = sp,
            Subject = validatedAuthnRequest.Subject!,
            ProtocolRequest = validatedAuthnRequest,
            RequestedClaimTypes = validatedAuthnRequest.RequestedClaimTypes
        };

        await profileService.GetProfileDataAsync(profileRequest, ct);

        var attributes = new List<SamlAttribute>(profileRequest.SamlAttributes);

        foreach (var mapped in MapClaimsToAttributes(profileRequest.IssuedClaims, sp))
        {
            var existing = attributes.Find(a => a.Name == mapped.Name);
            if (existing is not null)
            {
                existing.Values.AddRange(mapped.Values);
            }
            else
            {
                attributes.Add(mapped);
            }
        }

        return attributes;
    }

    /// <summary>
    /// Maps OIDC claims to SAML attribute. If a service provider specific mapping exists, that
    /// will be used. If one does not exist, the global mapping on <see cref="SamlOptions"/> will
    /// be used. If a claim does not have a mapping, it will be passed through as-is.
    /// </summary>
    /// <param name="claims">Claims issued from the call to <see cref="IProfileService"/></param>
    /// <param name="saml2Sp">The SAML service provider</param>
    /// <returns>The issued OIDC claims as mapped SamlAttributes</returns>
    protected virtual IList<SamlAttribute> MapClaimsToAttributes(IEnumerable<Claim> claims, SamlServiceProvider saml2Sp)
    {
        var claimsMappings = saml2Sp.ClaimMappings.Count > 0
            ? saml2Sp.ClaimMappings
            : _samlOptions.DefaultClaimMappings;

        List<SamlAttribute> attributes = [];

        foreach (var claim in claims)
        {
            var attributeName = claimsMappings.TryGetValue(claim.Type, out var mappedName)
                ? mappedName
                : claim.Type;

            var existing = attributes.Find(a => a.Name == attributeName);
            if (existing is not null)
            {
                existing.Values.Add(claim.Value);
            }
            else
            {
                attributes.Add(new SamlAttribute { Name = attributeName, Values = [claim.Value] });
            }
        }

        return attributes;
    }

    private static string ResolveAuthnContextClassRef(ClaimsPrincipal subject, IDictionary<string, string> mappings)
    {
        var acr = subject.FindFirstValue(JwtClaimTypes.AuthenticationContextClassReference);
        if (!string.IsNullOrWhiteSpace(acr) && mappings.TryGetValue(acr, out var acrMapped))
        {
            return acrMapped;
        }

        foreach (var amrClaim in subject.FindAll(JwtClaimTypes.AuthenticationMethod))
        {
            if (mappings.TryGetValue(amrClaim.Value, out var amrMapped))
            {
                return amrMapped;
            }
        }

        return SamlConstants.AuthnContextClasses.Unspecified;
    }
}
