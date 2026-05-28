// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Represents contextual information about a SAML authentication request,
/// extracted from <see cref="SamlAuthenticationState"/> for use by login UI pages.
/// Mirrors the OIDC <c>AuthorizationRequest</c> pattern.
/// </summary>
public sealed class SamlAuthenticationContext : IAuthenticationContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="SamlAuthenticationContext"/> for testing.
    /// </summary>
    public SamlAuthenticationContext()
    {
        ServiceProvider = default!;
        IdP = null;
        LoginHint = null;
        Tenant = null;
        PromptModes = [];
        RelayState = null;
        IsIdpInitiated = false;
        RequestedAuthnContext = null;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SamlAuthenticationContext"/> from
    /// a <see cref="SamlAuthenticationState"/>, the associated <see cref="SamlServiceProvider"/>,
    /// and the state identifier needed to write back denial information.
    /// </summary>
    /// <param name="state">The stored SAML authentication state.</param>
    /// <param name="serviceProvider">The service provider that initiated the request.</param>
    /// <param name="stateId">The identifier of the stored state entry.</param>
    public SamlAuthenticationContext(SamlAuthenticationState state, SamlServiceProvider serviceProvider, Guid stateId)
    {
        ServiceProvider = serviceProvider;
        RelayState = state.RelayState;
        IsIdpInitiated = state.IsIdpInitiated;

        var data = state.AuthnRequestData;
        IdP = data?.IdpHintProviderId;
        LoginHint = data?.SubjectNameIdValue;
        Tenant = ExtractTenant(data?.RequestedAuthnContext);
        PromptModes = BuildPromptModes(data);
        RequestedAuthnContext = data?.RequestedAuthnContext;
        StateId = stateId;
    }

    /// <summary>
    /// The service provider that initiated the authentication request.
    /// </summary>
    public SamlServiceProvider ServiceProvider { get; init; }

    /// <inheritdoc />
    IConnectedApplication IAuthenticationContext.Application => ServiceProvider;

    /// <inheritdoc />
    public string? IdP { get; init; }

    /// <inheritdoc />
    public string? LoginHint { get; init; }

    /// <inheritdoc />
    public string? Tenant { get; init; }

    /// <inheritdoc />
    public IEnumerable<string> PromptModes { get; init; }

    /// <summary>
    /// The RelayState parameter from the original SAML request.
    /// </summary>
    public string? RelayState { get; init; }

    /// <summary>
    /// Indicates whether this is an IdP-initiated SSO flow (no AuthnRequest).
    /// </summary>
    public bool IsIdpInitiated { get; init; }

    /// <summary>
    /// The identifier of the stored SAML authentication state entry.
    /// Used by <see cref="IIdentityServerInteractionService.DenyAuthenticationAsync"/> to write
    /// denial information back to the state store.
    /// </summary>
    public Guid StateId { get; init; }

    /// <summary>
    /// The requested authentication context from the AuthnRequest, if present.
    /// </summary>
    public StoredRequestedAuthnContext? RequestedAuthnContext { get; init; }

    private static string? ExtractTenant(StoredRequestedAuthnContext? requestedAuthnContext)
    {
        if (requestedAuthnContext is null)
        {
            return null;
        }

        var tenantRef = requestedAuthnContext.AuthnContextClassRef
            .FirstOrDefault(r => r.StartsWith(Constants.KnownAcrValues.Tenant, StringComparison.Ordinal));

        return tenantRef?[Constants.KnownAcrValues.Tenant.Length..];
    }

    private static List<string> BuildPromptModes(StoredAuthnRequestData? data)
    {
        if (data is null)
        {
            return [];
        }

        var modes = new List<string>();
        if (data.ForceAuthn)
        {
            modes.Add(OidcConstants.PromptModes.Login);
        }
        if (data.IsPassive)
        {
            modes.Add(OidcConstants.PromptModes.None);
        }
        return modes;
    }
}
