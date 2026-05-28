// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Entities = Duende.IdentityServer.EntityFramework.Entities;
using Models = Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Mappers;

public class SamlSigninStateMappersTests
{
    private readonly ISamlSigninStateSerializer _serializer = new DefaultSamlSigninStateSerializer();

    private static SamlAuthenticationState CreateModel(bool withAuthnRequest = false) =>
        new()
        {
            ServiceProviderEntityId = "https://sp.example.com",
            RelayState = "some-relay-state",
            IsIdpInitiated = false,
            CreatedUtc = DateTimeOffset.UtcNow,
            AssertionConsumerService = new Models.IndexedEndpoint
            {
                Binding = SamlBinding.HttpPost,
                Location = "https://sp.example.com/acs",
                Index = 1,
                IsDefault = true,
            },
            AuthnRequestData = withAuthnRequest
                ? new StoredAuthnRequestData
                {
                    RequestId = "request-id",
                    ForceAuthn = true,
                    IsPassive = false,
                    NameIdPolicyFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SubjectNameIdValue = "user@example.com",
                    IdpHintProviderId = "https://idp.example.com",
                    RequestedAuthnContext = new StoredRequestedAuthnContext
                    {
                        Comparison = "exact",
                        AuthnContextClassRef = ["urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport"],
                        AuthnContextDeclRef = ["urn:custom:decl"],
                    },
                }
                : null,
        };

    [Fact]
    public void can_map_saml_signin_state()
    {
        var model = CreateModel();
        var stateId = Guid.NewGuid();

        var entity = model.ToEntity(stateId, DateTime.UtcNow.AddMinutes(15), _serializer);
        var roundTripped = entity.ToModel(_serializer);

        entity.ShouldNotBeNull();
        roundTripped.ShouldNotBeNull();
    }

    [Fact]
    public void round_trip_preserves_all_properties()
    {
        var model = CreateModel(withAuthnRequest: true);
        var stateId = Guid.NewGuid();

        var entity = model.ToEntity(stateId, DateTime.UtcNow.AddMinutes(15), _serializer);
        var roundTripped = entity.ToModel(_serializer)!;

        roundTripped.ServiceProviderEntityId.ShouldBe(model.ServiceProviderEntityId);
        roundTripped.RelayState.ShouldBe(model.RelayState);
        roundTripped.IsIdpInitiated.ShouldBe(model.IsIdpInitiated);
        roundTripped.CreatedUtc.ShouldBe(model.CreatedUtc, TimeSpan.FromMilliseconds(1));
        roundTripped.AssertionConsumerService.Binding.ShouldBe(model.AssertionConsumerService.Binding);
        roundTripped.AssertionConsumerService.Location.ShouldBe(model.AssertionConsumerService.Location);
        roundTripped.AssertionConsumerService.Index.ShouldBe(model.AssertionConsumerService.Index);
        roundTripped.AssertionConsumerService.IsDefault.ShouldBe(model.AssertionConsumerService.IsDefault);
        roundTripped.AuthnRequestData.ShouldNotBeNull();
        roundTripped.AuthnRequestData!.RequestId.ShouldBe(model.AuthnRequestData!.RequestId);
    }

    [Fact]
    public void to_entity_sets_state_id()
    {
        var model = CreateModel();
        var stateId = Guid.NewGuid();

        var entity = model.ToEntity(stateId, DateTime.UtcNow.AddMinutes(15), _serializer);

        entity.StateId.ShouldBe(stateId);
    }

    [Fact]
    public void to_entity_sets_expires_at_utc()
    {
        var model = CreateModel();
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(15);
        var entity = model.ToEntity(Guid.NewGuid(), expiresAtUtc, _serializer);

        entity.ExpiresAtUtc.ShouldBe(expiresAtUtc);
    }

    [Fact]
    public void to_entity_sets_service_provider_entity_id()
    {
        var model = CreateModel();
        var entity = model.ToEntity(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(15), _serializer);

        entity.ServiceProviderEntityId.ShouldBe(model.ServiceProviderEntityId);
    }

    [Fact]
    public void round_trip_preserves_authn_request_data()
    {
        var model = CreateModel(withAuthnRequest: true);
        var stateId = Guid.NewGuid();

        var entity = model.ToEntity(stateId, DateTime.UtcNow.AddMinutes(15), _serializer);
        var roundTripped = entity.ToModel(_serializer)!;

        roundTripped.AuthnRequestData.ShouldNotBeNull();
        var data = roundTripped.AuthnRequestData!;
        data.RequestId.ShouldBe("request-id");
        data.ForceAuthn.ShouldBeTrue();
        data.IsPassive.ShouldBeFalse();
        data.NameIdPolicyFormat.ShouldBe("urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress");
        data.SubjectNameIdValue.ShouldBe("user@example.com");
        data.IdpHintProviderId.ShouldBe("https://idp.example.com");
        data.RequestedAuthnContext.ShouldNotBeNull();
        data.RequestedAuthnContext!.Comparison.ShouldBe("exact");
        data.RequestedAuthnContext.AuthnContextClassRef.ShouldBe(["urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport"]);
        data.RequestedAuthnContext.AuthnContextDeclRef.ShouldBe(["urn:custom:decl"]);
    }

    [Fact]
    public void to_model_returns_null_for_null_entity()
    {
        Entities.SamlSigninState? entity = null;
        var model = entity.ToModel(_serializer);
        model.ShouldBeNull();
    }
}
