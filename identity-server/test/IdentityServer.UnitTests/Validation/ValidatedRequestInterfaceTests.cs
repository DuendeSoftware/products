// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Validation;

namespace IdentityServer.UnitTests.Validation;

public sealed class ValidatedRequestInterfaceTests
{
    [Fact]
    public void ValidatedRequest_Implements_IValidatedRequest()
    {
        var request = new ValidatedRequest();

        request.ShouldBeAssignableTo<IValidatedRequest>();
    }

    [Fact]
    public void Application_Maps_To_Client()
    {
        var client = new Client { ClientId = "oidc-client", ClientName = "OIDC Client" };
        var request = new ValidatedRequest { Client = client };

        var validated = (IValidatedRequest)request;

        validated.Application.ShouldBeSameAs(client);
        validated.Application.ShouldBeAssignableTo<IConnectedApplication>();
        validated.Application!.Identifier.ShouldBe("oidc-client");
        validated.Application.DisplayName.ShouldBe("OIDC Client");
    }

    [Fact]
    public void Application_Is_Null_When_Client_Is_Null()
    {
        var request = new ValidatedRequest { Client = null! };

        var validated = (IValidatedRequest)request;

        validated.Application.ShouldBeNull();
    }

    [Fact]
    public void Subject_Passes_Through()
    {
        var subject = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "alice")], "test"));
        var request = new ValidatedRequest { Subject = subject };

        var validated = (IValidatedRequest)request;

        validated.Subject.ShouldBeSameAs(subject);
    }

    [Fact]
    public void Subject_Is_Null_When_Not_Set()
    {
        var request = new ValidatedRequest();

        var validated = (IValidatedRequest)request;

        validated.Subject.ShouldBeNull();
    }

    [Fact]
    public void SessionId_Passes_Through()
    {
        var request = new ValidatedRequest { SessionId = "session-123" };

        var validated = (IValidatedRequest)request;

        validated.SessionId.ShouldBe("session-123");
    }

    [Fact]
    public void SessionId_Is_Null_When_Not_Set()
    {
        var request = new ValidatedRequest();

        var validated = (IValidatedRequest)request;

        validated.SessionId.ShouldBeNull();
    }

    [Fact]
    public void Does_Not_Expose_Application_On_Public_Api()
    {
        // Application is explicit interface only — preserves the Client-centric public API.
        var type = typeof(ValidatedRequest);
        type.GetProperty("Application").ShouldBeNull();
    }

    [Fact]
    public void Pattern_Matching_On_Application_Distinguishes_Client()
    {
        var client = new Client { ClientId = "oidc-client" };
        var request = new ValidatedRequest { Client = client };

        IValidatedRequest validated = request;

        var identifier = validated.Application switch
        {
            Client c => c.ClientId,
            SamlServiceProvider sp => sp.EntityId,
            _ => "unknown"
        };

        identifier.ShouldBe("oidc-client");
    }

    [Fact]
    public void IssuerName_Passes_Through()
    {
        var request = new ValidatedRequest { IssuerName = "https://idp.example.com" };

        var validated = (IValidatedRequest)request;

        validated.IssuerName.ShouldBe("https://idp.example.com");
    }

    [Fact]
    public void Options_Passes_Through()
    {
        var options = new IdentityServerOptions { IssuerUri = "https://idp.example.com" };
        var request = new ValidatedRequest { Options = options };

        var validated = (IValidatedRequest)request;

        validated.Options.ShouldBeSameAs(options);
    }

    [Fact]
    public void Saml_IssuerName_Maps_To_Saml2IdpEntityId()
    {
        var request = new ValidatedAuthnRequest
        {
            IdentityServerOptions = new IdentityServerOptions(),
            Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST",
            Saml2IdpEntityId = "https://saml-idp.example.com"
        };

        var validated = (IValidatedRequest)request;

        validated.IssuerName.ShouldBe("https://saml-idp.example.com");
    }

    [Fact]
    public void Saml_Options_Maps_To_IdentityServerOptions()
    {
        var options = new IdentityServerOptions { IssuerUri = "https://saml-idp.example.com" };
        var request = new ValidatedAuthnRequest
        {
            IdentityServerOptions = options,
            Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST",
            Saml2IdpEntityId = "https://saml-idp.example.com"
        };

        var validated = (IValidatedRequest)request;

        validated.Options.ShouldBeSameAs(options);
    }

    [Fact]
    public void IssuerName_Is_Default_When_Not_Set()
    {
        var request = new ValidatedRequest();

        var validated = (IValidatedRequest)request;

        validated.IssuerName.ShouldBeNull();
    }

    [Fact]
    public void Options_Is_Default_When_Not_Set()
    {
        var request = new ValidatedRequest();

        var validated = (IValidatedRequest)request;

        validated.Options.ShouldBeNull();
    }
}
