// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Microsoft.AspNetCore.Http;

namespace UnitTests.Saml;

public sealed class IdpInitiatedSsoResultTests
{
    [Fact]
    public void SuccessWithIResultSetsProperties()
    {
        var stubResult = new StubResult();
        var result = IdpInitiatedSsoResult.Success(stubResult, "https://sp.example.com");

        result.IsError.ShouldBeFalse();
        result.Error.ShouldBeNull();
        result.Response.ShouldBe(stubResult);
        result.SpEntityId.ShouldBe("https://sp.example.com");
    }

    [Fact]
    public void SuccessWithNullResponseThrows() =>
        Should.Throw<ArgumentNullException>(() =>
            IdpInitiatedSsoResult.Success((IResult)null!, "https://sp.example.com"));

    [Fact]
    public void FailureSetsProperties()
    {
        var result = IdpInitiatedSsoResult.Failure("something went wrong", "https://sp.example.com");

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("something went wrong");
        result.Response.ShouldBeNull();
        result.SpEntityId.ShouldBe("https://sp.example.com");
    }

    [Fact]
    public void FailureWithoutSpEntityIdSetsProperties()
    {
        var result = IdpInitiatedSsoResult.Failure("something went wrong");

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe("something went wrong");
        result.Response.ShouldBeNull();
        result.SpEntityId.ShouldBeNull();
    }

    [Fact]
    public void FailureWithNullErrorThrows() =>
        Should.Throw<ArgumentNullException>(() =>
            IdpInitiatedSsoResult.Failure(null!));

    [Fact]
    public void FailureWithWhitespaceErrorThrows() =>
        Should.Throw<ArgumentException>(() =>
            IdpInitiatedSsoResult.Failure("  "));

    [Fact]
    public void SuccessWithWhitespaceSpEntityIdThrows() =>
        Should.Throw<ArgumentException>(() =>
            IdpInitiatedSsoResult.Success(new StubResult(), "  "));

    private sealed class StubResult : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext) => Task.CompletedTask;
    }
}

public sealed class SamlAutoPostResultTests
{
    [Fact]
    public void ConstructorWithNullThrows() =>
        Should.Throw<ArgumentNullException>(() => new SamlAutoPostResult(null!));

    [Fact]
    public void FrontChannelResultExposesInner()
    {
        var inner = CreateFrontChannelResult();
        var sut = new SamlAutoPostResult(inner);

        sut.FrontChannelResult.ShouldBe(inner);
    }

    private static Saml2FrontChannelResult CreateFrontChannelResult()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<SAMLResponse/>");

        return new Saml2FrontChannelResult
        {
            Message = new OutboundSaml2Message
            {
                Name = "SAMLResponse",
                Xml = doc.DocumentElement!,
                Destination = "https://sp.example.com/acs",
                Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"
            }
        };
    }
}
