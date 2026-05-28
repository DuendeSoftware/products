// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Events;

namespace UnitTests.Events;

public sealed class SamlEventTests
{
    [Fact]
    public void SamlSsoSuccessEvent_ShouldSetProperties()
    {
        var evt = new SamlSsoSuccessEvent("https://sp.example.com", "user123", "session-idx", "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST", "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified");

        evt.SpEntityId.ShouldBe("https://sp.example.com");
        evt.SubjectId.ShouldBe("user123");
        evt.SessionIndex.ShouldBe("session-idx");
        evt.Binding.ShouldBe("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST");
        evt.NameIdFormat.ShouldBe("urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified");
        evt.Category.ShouldBe(EventCategories.Saml);
        evt.EventType.ShouldBe(EventTypes.Success);
        evt.Id.ShouldBe(EventIds.SamlSsoSuccess);
    }

    [Fact]
    public void SamlSsoFailureEvent_ShouldSetProperties()
    {
        var evt = new SamlSsoFailureEvent("https://sp.example.com", "Validation failed", "SingleSignOnService");

        evt.SpEntityId.ShouldBe("https://sp.example.com");
        evt.Error.ShouldBe("Validation failed");
        evt.Endpoint.ShouldBe("SingleSignOnService");
        evt.Category.ShouldBe(EventCategories.Saml);
        evt.EventType.ShouldBe(EventTypes.Failure);
        evt.Id.ShouldBe(EventIds.SamlSsoFailure);
    }

    [Fact]
    public void SamlSsoFailureEvent_NullSpEntityId_ShouldBeAllowed()
    {
        var evt = new SamlSsoFailureEvent(null, "No binding found", "SingleSignOnService");

        evt.SpEntityId.ShouldBeNull();
    }

    [Fact]
    public void SamlSloSuccessEvent_ShouldSetProperties()
    {
        var evt = new SamlSloSuccessEvent("https://sp.example.com", "session-idx", "SP");

        evt.SpEntityId.ShouldBe("https://sp.example.com");
        evt.SessionIndex.ShouldBe("session-idx");
        evt.Initiator.ShouldBe("SP");
        evt.Category.ShouldBe(EventCategories.Saml);
        evt.EventType.ShouldBe(EventTypes.Success);
        evt.Id.ShouldBe(EventIds.SamlSloSuccess);
    }

    [Fact]
    public void SamlSloFailureEvent_ShouldSetProperties()
    {
        var evt = new SamlSloFailureEvent("https://sp.example.com", "Validation failed");

        evt.SpEntityId.ShouldBe("https://sp.example.com");
        evt.Error.ShouldBe("Validation failed");
        evt.Category.ShouldBe(EventCategories.Saml);
        evt.EventType.ShouldBe(EventTypes.Failure);
        evt.Id.ShouldBe(EventIds.SamlSloFailure);
    }

    [Fact]
    public void SamlAuthnRequestValidationFailureEvent_ShouldSetProperties()
    {
        var evt = new SamlAuthnRequestValidationFailureEvent("https://sp.example.com", "Invalid issuer", "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect");

        evt.SpEntityId.ShouldBe("https://sp.example.com");
        evt.Error.ShouldBe("Invalid issuer");
        evt.Binding.ShouldBe("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect");
        evt.Category.ShouldBe(EventCategories.Saml);
        evt.EventType.ShouldBe(EventTypes.Failure);
        evt.Id.ShouldBe(EventIds.SamlAuthnRequestValidationFailure);
    }

    [Fact]
    public void SamlAuthnRequestValidationFailureEvent_NullableFields_ShouldBeAllowed()
    {
        var evt = new SamlAuthnRequestValidationFailureEvent(null, "Parse error", null);

        evt.SpEntityId.ShouldBeNull();
        evt.Binding.ShouldBeNull();
    }

    [Fact]
    public void SamlLogoutRequestValidationFailureEvent_ShouldSetProperties()
    {
        var evt = new SamlLogoutRequestValidationFailureEvent("https://sp.example.com", "Invalid signature", "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect");

        evt.SpEntityId.ShouldBe("https://sp.example.com");
        evt.Error.ShouldBe("Invalid signature");
        evt.Binding.ShouldBe("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect");
        evt.Category.ShouldBe(EventCategories.Saml);
        evt.EventType.ShouldBe(EventTypes.Failure);
        evt.Id.ShouldBe(EventIds.SamlLogoutRequestValidationFailure);
    }

    [Fact]
    public void SamlLogoutRequestValidationFailureEvent_NullableFields_ShouldBeAllowed()
    {
        var evt = new SamlLogoutRequestValidationFailureEvent(null, "Parse error", null);

        evt.SpEntityId.ShouldBeNull();
        evt.Binding.ShouldBeNull();
    }
}
