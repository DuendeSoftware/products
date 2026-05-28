// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Saml.Validation;

/// <summary>
/// Validated AuthnRequest
/// </summary>
public class ValidatedAuthnRequest : IValidatedRequest
{
    /// <summary>
    /// The current IdentityServerOptions
    /// </summary>
    public required IdentityServerOptions IdentityServerOptions { get; init; }

    /// <summary>
    /// The AuthnRequest. Null for IdP-initiated SSO flows.
    /// </summary>
    public AuthnRequest? AuthnRequest { get; init; }

    /// <summary>
    /// Identifier of binding used to read the AuthnRequest
    /// </summary>
    public required string Binding { get; init; }

    /// <summary>
    /// The original inbound SAML message from the binding layer. Present on the SSO
    /// endpoint path where the raw request is available; null on the callback path
    /// where we are working from stored state. Binding-level signatures cannot be
    /// replayed after the redirect to login, so re-validation from the raw message
    /// is not possible for SAML (unlike OIDC, which re-validates from stored parameters).
    /// </summary>
    public InboundSaml2Message? Saml2Message { get; init; }

    /// <summary>
    /// The RelayState parameter from the original SAML request. Stored separately
    /// so it is available on both the SSO endpoint path (from the binding) and the
    /// callback path (from persisted state) without requiring the full Saml2Message.
    /// </summary>
    public string? RelayState { get; init; }

    /// <summary>
    /// The Saml2 SP
    /// </summary>
    public SamlServiceProvider? Saml2Sp { get; set; }

    /// <inheritdoc />
    public IConnectedApplication? Application => Saml2Sp;

    /// <inheritdoc />
#pragma warning disable CA1033 // Explicit interface impl is intentional — SAML code uses concrete properties directly
    string IValidatedRequest.IssuerName => Saml2IdpEntityId;

    /// <inheritdoc />
    IdentityServerOptions IValidatedRequest.Options => IdentityServerOptions;
#pragma warning restore CA1033

    /// <summary>
    /// The current user
    /// </summary>
    public ClaimsPrincipal? Subject { get; init; }

    /// <summary>
    /// The current SessionId
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// The Saml2 identifier for IdentityServer
    /// </summary>
    public required string Saml2IdpEntityId { get; set; }

    /// <summary>
    /// AssertionConsumerService to respond to, set once we have enough validation to be able
    /// to trust it and return error responses to it.
    /// </summary>
    public IndexedEndpoint? AssertionConsumerService { get; set; }

    /// <summary>
    /// Resource "validation" results. Used to get list of claims to include in response.
    /// </summary>
    public ResourceValidationResult? ValidatedResources { get; set; }

    /// <summary>
    /// The claim types to request from the profile service for this assertion.
    /// Set during resource validation based on the SP's configuration.
    /// </summary>
    public IReadOnlyList<string> RequestedClaimTypes { get; set; } = [];

    /// <summary>
    /// Indicates whether this request originates from an IdP-initiated SSO flow.
    /// When true, the SAML response MUST NOT include an InResponseTo attribute
    /// per SAML 2.0 Profiles §4.1.4.5.
    /// </summary>
    public bool IsIdpInitiated { get; init; }

    /// <summary>
    /// The session index to include in the AuthnStatement. This value is used by
    /// the SP to correlate the assertion with a specific session for single logout.
    /// </summary>
    public string? SessionIndex { get; set; }

    /// <summary>
    /// The AuthnRequest ID, used for InResponseTo in the SAML response.
    /// On the SSO endpoint path, populated from the parsed <see cref="AuthnRequest"/>.
    /// On the callback path, rehydrated from persisted state.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// The NameIdPolicy Format requested by the SP.
    /// On the SSO endpoint path, populated from the parsed <see cref="AuthnRequest"/>.
    /// On the callback path, rehydrated from persisted state.
    /// </summary>
    public string? NameIdPolicyFormat { get; set; }
}
