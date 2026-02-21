// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text;
using Duende.IdentityModel.Client;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using UnitTests.Common;

namespace UnitTests.Validation.Secrets;

public class BasicAuthenticationSecretParsing
{
    private const string Category = "Secrets - Basic Authentication Secret Parsing";
    private readonly CT _ct = TestContext.Current.CancellationToken;

    private IdentityServerOptions _options;
    private BasicAuthenticationSecretParser _parser;

    public BasicAuthenticationSecretParsing()
    {
        _options = new IdentityServerOptions();
        _parser = new BasicAuthenticationSecretParser(_options, TestLogger.Create<BasicAuthenticationSecretParser>());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EmptyContext()
    {
        var context = new DefaultHttpContext();

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_BasicAuthentication_Request()
    {
        var context = new DefaultHttpContext();

        var headerValue = string.Format("Basic {0}",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("client:secret")));
        context.Request.Headers.Append("Authorization", new StringValues(headerValue));


        var secret = await _parser.ParseAsync(context, _ct);

        secret.Type.ShouldBe(IdentityServerConstants.ParsedSecretTypes.SharedSecret);
        secret.Id.ShouldBe("client");
        secret.Credential.ShouldBe("secret");
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData("client", "secret")]
    [InlineData("cl ient", "secret")]
    [InlineData("cl ient", "se cret")]
    [InlineData("client", "se+cret")]
    [InlineData("cl+ ient", "se+cret")]
    [InlineData("cl+ ient", "se+ cret")]
    [InlineData("client:urn", "secret")]
    public async Task Valid_BasicAuthentication_Request_in_various_Formats_Manual(string userName, string password)
    {
        var encoding = Encoding.UTF8;
        var context = new DefaultHttpContext();

        if (password == null)
        {
            password = "";
        }

        var credential = $"{Uri.EscapeDataString(userName)}:{Uri.EscapeDataString(password)}";

        var headerValue = $"Basic {Convert.ToBase64String(encoding.GetBytes(credential))}";
        context.Request.Headers.Append("Authorization", new StringValues(headerValue));

        var secret = await _parser.ParseAsync(context, _ct);

        secret.Type.ShouldBe(IdentityServerConstants.ParsedSecretTypes.SharedSecret);
        secret.Id.ShouldBe(userName);
        secret.Credential.ShouldBe(password);
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData("client", "secret")]
    [InlineData("cl ient", "secret")]
    [InlineData("cl ient", "se cret")]
    [InlineData("client", "se+cret")]
    [InlineData("cl+ ient", "se+cret")]
    [InlineData("cl+ ient", "se+ cret")]
    [InlineData("client:urn", "secret")]
    public async Task Valid_BasicAuthentication_Request_in_various_Formats_IdentityModel(string userName, string password)
    {
        var encoding = Encoding.UTF8;
        var context = new DefaultHttpContext();

        var credential = BasicAuthenticationOAuthHeaderValue.EncodeCredential(userName, password);
        var headerValue = $"Basic {credential}";
        context.Request.Headers.Append("Authorization", new StringValues(headerValue));

        var secret = await _parser.ParseAsync(context, _ct);

        secret.Type.ShouldBe(IdentityServerConstants.ParsedSecretTypes.SharedSecret);
        secret.Id.ShouldBe(userName);
        secret.Credential.ShouldBe(password);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_BasicAuthentication_Request_With_UserName_Only_And_Colon_For_Optional_ClientSecret()
    {
        var context = new DefaultHttpContext();

        var headerValue = string.Format("Basic {0}",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("client:")));
        context.Request.Headers.Append("Authorization", new StringValues(headerValue));

        var secret = await _parser.ParseAsync(context, _ct);

        secret.Type.ShouldBe(IdentityServerConstants.ParsedSecretTypes.NoSecret);
        secret.Id.ShouldBe("client");
        secret.Credential.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task BasicAuthentication_Request_With_Empty_Basic_Header()
    {
        var context = new DefaultHttpContext();

        context.Request.Headers.Append("Authorization", new StringValues(string.Empty));

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_BasicAuthentication_Request_ClientId_Too_Long()
    {
        var context = new DefaultHttpContext();

        var longClientId = "x".Repeat(_options.InputLengthRestrictions.ClientId + 1);
        var credential = string.Format("{0}:secret", longClientId);

        var headerValue = string.Format("Basic {0}",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(credential)));
        context.Request.Headers.Append("Authorization", new StringValues(headerValue));

        var secret = await _parser.ParseAsync(context, _ct);
        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_BasicAuthentication_Request_ClientSecret_Too_Long()
    {
        var context = new DefaultHttpContext();

        var longClientSecret = "x".Repeat(_options.InputLengthRestrictions.ClientSecret + 1);
        var credential = string.Format("client:{0}", longClientSecret);

        var headerValue = string.Format("Basic {0}",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(credential)));
        context.Request.Headers.Append("Authorization", new StringValues(headerValue));

        var secret = await _parser.ParseAsync(context, _ct);
        secret.ShouldBeNull();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(102)]
    [InlineData(103)]
    [InlineData(104)]
    [InlineData(105)]
    [InlineData(106)]
    [InlineData(107)]
    [InlineData(108)]
    [Trait("Category", Category)]
    public async Task Valid_BasicAuthentication_Request_Maximum_Url_Encoded_Values_Should_Work(int maxLength)
    {
        var parser = CreateParser(maxLength);

        var context = new DefaultHttpContext();

        var clientId = "!".Repeat(maxLength);
        var clientSecret = "#".Repeat(maxLength);
        var credential = $"{Uri.EscapeDataString(clientId)}:{Uri.EscapeDataString(clientSecret)}";

        var headerValue = string.Format("Basic {0}",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(credential)));
        context.Request.Headers.Append("Authorization", new StringValues(headerValue));

        var secret = await parser.ParseAsync(context, _ct);
        secret.Id.ShouldBe(clientId);
        secret.Credential.ShouldBe(clientSecret);
    }

    private static BasicAuthenticationSecretParser CreateParser(int maxLength)
    {
        var options = new IdentityServerOptions
        {
            InputLengthRestrictions = new InputLengthRestrictions
            {
                ClientId = maxLength,
                ClientSecret = maxLength
            }
        };
        var parser = new BasicAuthenticationSecretParser(options, TestLogger.Create<BasicAuthenticationSecretParser>());
        return parser;
    }

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(102)]
    [InlineData(103)]
    [InlineData(104)]
    [InlineData(105)]
    [InlineData(106)]
    [InlineData(107)]
    [InlineData(108)]
    [Trait("Category", Category)]
    public async Task Valid_BasicAuthentication_Request_Authorization_Header_Too_Long_Should_Fail(int maxLength)
    {
        var parser = CreateParser(maxLength);

        var context = new DefaultHttpContext();

        var clientId = Uri.EscapeDataString("!".Repeat(maxLength));
        var clientSecret = Uri.EscapeDataString("#".Repeat(maxLength)) + "x";
        var credential = $"{clientId}:{clientSecret}";

        var headerValue = string.Format("Basic {0}",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(credential)));
        context.Request.Headers.Append("Authorization", new StringValues(headerValue));

        var secret = await parser.ParseAsync(context, _ct);
        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task BasicAuthentication_Request_With_Empty_Basic_Header_Variation()
    {
        var context = new DefaultHttpContext();

        context.Request.Headers.Append("Authorization", new StringValues("Basic "));

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task BasicAuthentication_Request_With_Unknown_Scheme()
    {
        var context = new DefaultHttpContext();

        context.Request.Headers.Append("Authorization", new StringValues("Unknown"));

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task BasicAuthentication_Request_With_Malformed_Credentials_NoBase64_Encoding()
    {
        var context = new DefaultHttpContext();

        context.Request.Headers.Append("Authorization", new StringValues("Basic somerandomdata"));

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task BasicAuthentication_Request_With_Malformed_Credentials_Base64_Encoding_UserName_Only()
    {
        var context = new DefaultHttpContext();

        var headerValue = string.Format("Basic {0}",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("client")));
        context.Request.Headers.Append("Authorization", new StringValues(headerValue));

        var secret = await _parser.ParseAsync(context, _ct);

        secret.ShouldBeNull();
    }
}
