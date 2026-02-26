// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace UnitTests.Validation.Secrets;

public class FormPostCredentialExtraction
{
    private const string Category = "Secrets - Form Post Secret Parsing";
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private IdentityServerOptions _options;
    private PostBodySecretParser _parser;

    public FormPostCredentialExtraction()
    {
        _options = new IdentityServerOptions();
        _parser = new PostBodySecretParser(_options, new LoggerFactory().CreateLogger<PostBodySecretParser>());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EmptyContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream();

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_PostBody()
    {
        var context = new DefaultHttpContext();

        var body = "client_id=client&client_secret=secret";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.Type.ShouldBe(IdentityServerConstants.ParsedSecretTypes.SharedSecret);
        secret.Id.ShouldBe("client");
        secret.Credential.ShouldBe("secret");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ClientId_Too_Long()
    {
        var context = new DefaultHttpContext();

        var longClientId = "x".Repeat(_options.InputLengthRestrictions.ClientId + 1);
        var body = string.Format("client_id={0}&client_secret=secret", longClientId);

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ClientSecret_Too_Long()
    {
        var context = new DefaultHttpContext();

        var longClientSecret = "x".Repeat(_options.InputLengthRestrictions.ClientSecret + 1);
        var body = string.Format("client_id=client&client_secret={0}", longClientSecret);

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_ClientId()
    {
        var context = new DefaultHttpContext();

        var body = "client_secret=secret";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_ClientSecret()
    {
        var context = new DefaultHttpContext();

        var body = "client_id=client";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldNotBeNull();
        secret.Type.ShouldBe(IdentityServerConstants.ParsedSecretTypes.NoSecret);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Malformed_PostBody()
    {
        var context = new DefaultHttpContext();

        var body = "malformed";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }
}
