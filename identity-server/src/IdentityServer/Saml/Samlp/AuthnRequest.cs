// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml.Samlp;

/// <summary>
/// Authentication request
/// </summary>
public class AuthnRequest : RequestAbstractType
{
    /// <summary>
    /// The assertion consumer service Url where the response should be posted back to.
    /// </summary>
    public string? AssertionConsumerServiceUrl { get; set; }

    /// <summary>
    /// Identifying Uri for protocol binding
    /// </summary>
    public string? ProtocolBinding { get; set; }

    /// <summary>
    /// Requested Subject
    /// </summary>
    public Subject? Subject { get; set; }

    /// <summary>
    /// Specifies constraints on the name identifier to be used to represent the requested subject.
    /// </summary>
    public NameIdPolicy? NameIdPolicy { get; set; }

    /// <summary>
    /// Specifies the SAML conditions the requester expects to limit the validity and/or use of the resulting assertion(s)
    /// </summary>
    public Conditions? Conditions { get; set; }

    /// <summary>
    /// Specifies a set of identity providers trusted by the requester to authenticate the
    /// presenter, as well as limitations and context related to proxying
    /// </summary>
    public RequestedAuthnContext? RequestedAuthnContext { get; set; }

    /// <summary>
    /// The scoping identifies the identity providers that are trusted by the requester to authenticate the presenter.
    /// </summary>
    public Scoping? Scoping { get; set; }

    /// <summary>
    /// Indicates that the identity provider should force (re)authentication and not
    /// rely on an existing session to do single sign on.
    /// </summary>
    public bool ForceAuthn { get; set; }

    /// <summary>
    /// Indicates that the identity provider should not show any UI nor require interaction.
    /// </summary>
    public bool IsPassive { get; set; }

    /// <summary>
    /// Indicates requested attributes by referencing an attribution consumer service in the SP metadata.
    /// </summary>
    public int? AttributeConsumingServiceIndex { get; set; }

    /// <summary>
    /// Index of assertion consumer service where the SP wants the response.
    /// </summary>
    public int? AssertionConsumerServiceIndex { get; set; }

    /// <summary>
    /// The name of the requester.
    /// </summary>
    public string? ProviderName { get; set; }
}
