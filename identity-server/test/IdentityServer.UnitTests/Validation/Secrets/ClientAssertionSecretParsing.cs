// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace UnitTests.Validation.Secrets;

public class ClientAssertionSecretParsing
{
    private IdentityServerOptions _options;
    private JwtBearerClientAssertionSecretParser _parser;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public ClientAssertionSecretParsing()
    {
        _options = new IdentityServerOptions();
        _parser = new JwtBearerClientAssertionSecretParser(_options, new LoggerFactory().CreateLogger<JwtBearerClientAssertionSecretParser>());
    }

    [Fact]
    public async Task EmptyContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream();
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    public async Task Valid_ClientAssertion()
    {
        var context = new DefaultHttpContext();

        var token = new JwtSecurityToken(issuer: "issuer", claims: new[] { new Claim("sub", "client") });
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var body = "client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer&client_assertion=" + tokenString;

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldNotBeNull();
        secret.Type.ShouldBe(IdentityServerConstants.ParsedSecretTypes.JwtBearer);
        secret.Id.ShouldBe("client");
        secret.Credential.ShouldBe(tokenString);
    }

    [Fact]
    public async Task Missing_ClientAssertionType()
    {
        var context = new DefaultHttpContext();

        var body = "client_id=client&client_assertion=token";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    public async Task Missing_ClientAssertion()
    {
        var context = new DefaultHttpContext();

        var body = "client_id=client&client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    public async Task Malformed_PostBody()
    {
        var context = new DefaultHttpContext();
        var body = "malformed";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    public async Task ClientId_TooLong()
    {
        var context = new DefaultHttpContext();

        var longClientId = "x".Repeat(_options.InputLengthRestrictions.ClientId + 1);
        var body = $"client_id={longClientId}&client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer&client_assertion=token";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    public async Task ClientAssertion_TooLong()
    {
        var context = new DefaultHttpContext();

        var longToken = "x".Repeat(_options.InputLengthRestrictions.Jwt + 1);
        var body = $"client_id=client&client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer&client_assertion={longToken}";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }
}
