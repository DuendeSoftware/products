// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IO;
using System.Text;
using System.Threading.Tasks;
using Duende.IdentityServer.Validation;
using Shouldly;
using UnitTests.Common;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace UnitTests.Validation;

public class BearerTokenUsageValidation
{
    private const string Category = "BearerTokenUsageValidator Tests";

    [Fact]
    [Trait("Category", Category)]
    public async Task No_Header_no_Body_Get()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";

        var validator = new BearerTokenUsageValidator(TestLogger.Create< BearerTokenUsageValidator>());
        var result = await validator.ValidateAsync(ctx);

        result.TokenFound.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task No_Header_no_Body_Post()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";

        var validator = new BearerTokenUsageValidator(TestLogger.Create<BearerTokenUsageValidator>());
        var result = await validator.ValidateAsync(ctx);

        result.TokenFound.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Non_Bearer_Scheme_Header()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Headers.Append("Authorization", new string[] { "Foo Bar" });

        var validator = new BearerTokenUsageValidator(TestLogger.Create<BearerTokenUsageValidator>());
        var result = await validator.ValidateAsync(ctx);

        result.TokenFound.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Empty_Bearer_Scheme_Header()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Headers.Append("Authorization", new string[] { "Bearer" });

        var validator = new BearerTokenUsageValidator(TestLogger.Create<BearerTokenUsageValidator>());
        var result = await validator.ValidateAsync(ctx);

        result.TokenFound.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Whitespaces_Bearer_Scheme_Header()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Headers.Append("Authorization", new string[] { "Bearer           " });

        var validator = new BearerTokenUsageValidator(TestLogger.Create<BearerTokenUsageValidator>());
        var result = await validator.ValidateAsync(ctx);

        result.TokenFound.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Bearer_Scheme_Header()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Headers.Append("Authorization", new string[] { "Bearer token" });

        var validator = new BearerTokenUsageValidator(TestLogger.Create<BearerTokenUsageValidator>());
        var result = await validator.ValidateAsync(ctx);

        result.TokenFound.ShouldBeTrue();
        result.Token.ShouldBe("token");
        result.UsageType.ShouldBe(BearerTokenUsageType.AuthorizationHeader);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Body_Post()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.ContentType = "application/x-www-form-urlencoded";
        var body = "access_token=token";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        var validator = new BearerTokenUsageValidator(TestLogger.Create<BearerTokenUsageValidator>());
        var result = await validator.ValidateAsync(ctx);

        result.TokenFound.ShouldBeTrue();
        result.Token.ShouldBe("token");
        result.UsageType.ShouldBe(BearerTokenUsageType.PostBody);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Body_Post_empty_Token()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.ContentType = "application/x-www-form-urlencoded";
        var body = "access_token=";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        var validator = new BearerTokenUsageValidator(TestLogger.Create<BearerTokenUsageValidator>());
        var result = await validator.ValidateAsync(ctx);

        result.TokenFound.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Body_Post_Whitespace_Token()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.ContentType = "application/x-www-form-urlencoded";
        var body = "access_token=                ";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        var validator = new BearerTokenUsageValidator(TestLogger.Create<BearerTokenUsageValidator>());
        var result = await validator.ValidateAsync(ctx);

        result.TokenFound.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Body_Post_no_Token()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.ContentType = "application/x-www-form-urlencoded";
        var body = "foo=bar";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        var validator = new BearerTokenUsageValidator(TestLogger.Create<BearerTokenUsageValidator>());
        var result = await validator.ValidateAsync(ctx);

        result.TokenFound.ShouldBeFalse();
    }
}