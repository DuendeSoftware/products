// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Endpoints.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Shouldly;
using Xunit;

namespace UnitTests.Endpoints.Results;

public class ProtectedResourceErrorResultTests
{
    private readonly ProtectedResourceErrorHttpWriter writer = new();

    [Fact]
    public void WwwAuthenticate_header_with_error_and_description_should_be_a_single_line()
    {
        var context = new DefaultHttpContext();

        writer.WriteHttpResponse(
            new ProtectedResourceErrorResult("oops", "big oops"),
            context
        );

        var wwwAuthHeader = context.Response.Headers[HeaderNames.WWWAuthenticate].ToString();
        wwwAuthHeader.ShouldBe(
            """
            Bearer realm="IdentityServer",error="oops",error_description="big oops"
            """);
    }

    [Fact]
    public void WwwAuthenticate_header_with_error_should_be_a_single_line()
    {
        var context = new DefaultHttpContext();

        writer.WriteHttpResponse(
            new ProtectedResourceErrorResult("oops"),
            context
        );

        var wwwAuthHeader = context.Response.Headers[HeaderNames.WWWAuthenticate].ToString();
        wwwAuthHeader.ShouldBe(
            """
            Bearer realm="IdentityServer",error="oops"
            """);
    }

    [Fact]
    public void WwwAuthenticate_header_should_always_be_a_single_string_value()
    {
        var context = new DefaultHttpContext();

        writer.WriteHttpResponse(
            new ProtectedResourceErrorResult("oops", "big oops"),
            context
        );

        var wwwAuthHeader = context.Response.Headers[HeaderNames.WWWAuthenticate];
        wwwAuthHeader.Count.ShouldBe(1);
    }
}
