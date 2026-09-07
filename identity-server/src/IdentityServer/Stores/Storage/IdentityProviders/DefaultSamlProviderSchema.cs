// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Stores.Storage.IdentityProviders;

/// <summary>
/// Built-in attribute definitions for SAML 2.0 identity providers.
/// These correspond to the properties used by <see cref="Models.SamlProvider"/> and are
/// automatically registered in the schema store so that SAML providers work out-of-the-box.
/// </summary>
internal static class DefaultSamlProviderSchema
{
    /// <summary>The entity ID of the remote SAML identity provider.</summary>
    public static readonly TypedAttributeDefinition<string> IdpEntityId =
        new(AttributeCode.Create("IdpEntityId"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>The URL of the Single Sign-On service on the remote identity provider.</summary>
    public static readonly TypedAttributeDefinition<string> SingleSignOnServiceUrl =
        new(AttributeCode.Create("SingleSignOnServiceUrl"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>The URL of the Single Logout service on the remote identity provider.</summary>
    public static readonly TypedAttributeDefinition<string> SingleLogoutServiceUrl =
        new(AttributeCode.Create("SingleLogoutServiceUrl"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>Base64-encoded X.509 certificate used to validate signatures from the remote IdP.</summary>
    public static readonly TypedAttributeDefinition<string> SigningCertificateBase64 =
        new(AttributeCode.Create("SigningCertificateBase64"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>Base64-encoded X.509 certificate (PKCS#12) used by the SP to sign outbound SAML messages.</summary>
    public static readonly TypedAttributeDefinition<string> SpSigningCertificateBase64 =
        new(AttributeCode.Create("SpSigningCertificateBase64"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>Optional password for the PKCS#12 SP signing certificate.</summary>
    public static readonly TypedAttributeDefinition<string> SpSigningCertificatePassword =
        new(AttributeCode.Create("SpSigningCertificatePassword"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>
    /// The SAML binding type for authentication requests. Accepted values: <c>"redirect"</c> (default) or <c>"post"</c>.
    /// </summary>
    public static readonly TypedAttributeDefinition<string> BindingType =
        new(AttributeCode.Create("BindingType"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>Optional override for the SP entity ID.</summary>
    public static readonly TypedAttributeDefinition<string> SpEntityId =
        new(AttributeCode.Create("SpEntityId"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>
    /// Whether to allow unsolicited (IdP-initiated) authentication responses.
    /// Stored as <c>"true"</c> or <c>"false"</c>. Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Defined as <c>string</c> (not <c>bool</c>) because <see cref="Models.SamlProvider"/> reads these
    /// values from the Properties dictionary via string comparison, and
    /// <see cref="EavPropertyMapper.ExtractStringProperties"/> must round-trip them as strings.
    /// </remarks>
    public static readonly TypedAttributeDefinition<string> AllowUnsolicitedAuthnResponse =
        new(AttributeCode.Create("AllowUnsolicitedAuthnResponse"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>
    /// Whether assertions from the identity provider must be signed.
    /// Stored as <c>"true"</c> or <c>"false"</c>. Defaults to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Defined as <c>string</c> (not <c>bool</c>) because <see cref="Models.SamlProvider"/> reads these
    /// values from the Properties dictionary via string comparison, and
    /// <see cref="EavPropertyMapper.ExtractStringProperties"/> must round-trip them as strings.
    /// </remarks>
    public static readonly TypedAttributeDefinition<string> WantAssertionsSigned =
        new(AttributeCode.Create("WantAssertionsSigned"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>The signing algorithm for outbound SAML requests.</summary>
    public static readonly TypedAttributeDefinition<string> OutboundSigningAlgorithm =
        new(AttributeCode.Create("OutboundSigningAlgorithm"), new ScalarAttributeType(ScalarDataType.String));

    /// <summary>
    /// The built-in schema for SAML identity providers, registered with schema ID <c>idp:saml</c>.
    /// </summary>
    public static readonly SchemaConfiguration Schema = new()
    {
        SchemaId = SchemaId.IdentityProvider("saml"),
        DisplayName = "SAML Identity Provider",
        Description = "Built-in schema for SAML 2.0 identity providers.",
        AttributeDefinitions =
        [
            IdpEntityId,
            SingleSignOnServiceUrl,
            SingleLogoutServiceUrl,
            SigningCertificateBase64,
            SpSigningCertificateBase64,
            SpSigningCertificatePassword,
            BindingType,
            SpEntityId,
            AllowUnsolicitedAuthnResponse,
            WantAssertionsSigned,
            OutboundSigningAlgorithm
        ]
    };
}
