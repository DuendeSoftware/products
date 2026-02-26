// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Saml.Models;

/// <summary>
/// Represents a SAML 2.0 AuthnRequest message sent by a Service Provider to request authentication.
/// </summary>
public record AuthNRequest : ISamlRequest
{
    public static string MessageName => "SAML signin request";

    /// <summary>
    /// Gets or sets the unique identifier for this request.
    /// Must be unique across all requests from this SP.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the SAML version. Must be "2.0".
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets the time instant of issue in UTC.
    /// </summary>
    public required DateTime IssueInstant { get; set; }

    /// <summary>
    /// Gets or sets the URI reference indicating the destination to which this request is directed.
    /// Should match the IdP's SSO endpoint URL.
    /// </summary>
    public Uri? Destination { get; set; }

    /// <summary>
    /// Gets or sets the consent obtained from the principal for sending this request.
    /// </summary>
    public string? Consent { get; set; }

    /// <summary>
    /// Gets or sets the entity identifier of the Service Provider making this request.
    /// This is the SP's entity ID from its metadata.
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the IdP must freshly obtain the authentication (not from cache).
    /// If true, the IdP must reauthenticate the user even if a session exists.
    /// Default: false
    /// </summary>
    public bool ForceAuthn { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the IdP should not actively interact with the user.
    /// If true, the IdP should not show UI to the user (authentication must be passive).
    /// Default: false
    /// </summary>
    public bool IsPassive { get; set; }

    /// <summary>
    /// Gets or sets the URL of the ACS endpoint where the response should be sent (optional).
    /// If specified, overrides the default ACS URL from SP metadata.
    /// </summary>
    public Uri? AssertionConsumerServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets the index of the ACS endpoint where the response should be sent (optional).
    /// References an indexed ACS endpoint in the SP's metadata.
    /// </summary>
    public int? AssertionConsumerServiceIndex { get; set; }

    /// <summary>
    /// Gets or sets the SAML protocol binding to use for the response (optional).
    /// Example: "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"
    /// </summary>
    public SamlBinding? ProtocolBinding { get; set; }

    /// <summary>
    /// Gets or sets the requested authentication context constraints.
    /// Specifies requirements/preferences for the authentication context the IdP should use.
    /// Optional - if null, no specific context is required.
    /// </summary>
    public RequestedAuthnContext? RequestedAuthnContext { get; set; }

    /// <summary>
    /// Gets or sets the requested NameID policy constraints from the SP.
    /// Specifies the format and characteristics of the name identifier to return.
    /// Optional - if null, no specific policy is requested.
    /// </summary>
    public NameIdPolicy? NameIdPolicy { get; set; }

    internal static class AttributeNames
    {
        public const string Id = "ID";
        public const string Version = "Version";
        public const string IssueInstant = "IssueInstant";
        public const string Destination = "Destination";
        public const string Consent = "Consent";
        public const string Issuer = "Issuer";
        public const string ForceAuthn = "ForceAuthn";
        public const string IsPassive = "IsPassive";
        public const string AssertionConsumerServiceUrl = "AssertionConsumerServiceURL";
        public const string AssertionConsumerServiceIndex = "AssertionConsumerServiceIndex";
        public const string ProtocolBinding = "ProtocolBinding";
    }

    internal static class ElementNames
    {
        public const string RequestedAuthnContext = "RequestedAuthnContext";
        public const string NameIdPolicy = "NameIDPolicy";
    }
}
