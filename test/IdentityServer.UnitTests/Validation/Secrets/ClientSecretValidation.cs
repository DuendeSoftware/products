// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IO;
using System.Text;
using System.Threading.Tasks;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using FluentAssertions;
using UnitTests.Validation.Setup;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace UnitTests.Validation.Secrets;

public class ClientSecretValidation
{
    private const string Category = "Secrets - Client Secret Validator";

    [Fact]
    [Trait("Category", Category)]
    public async Task confidential_client_with_correct_secret_should_be_able_to_request_token()
    {
        var validator = Factory.CreateClientSecretValidator();

        var context = new DefaultHttpContext();
        var body = "client_id=roclient&client_secret=secret";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var result = await validator.ValidateAsync(context);

        result.IsError.Should().BeFalse();
        result.Client.ClientId.Should().Be("roclient");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task confidential_client_with_incorrect_secret_should_not_be_able_to_request_token()
    {
        var validator = Factory.CreateClientSecretValidator();

        var context = new DefaultHttpContext();
        var body = "client_id=roclient&client_secret=invalid";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var result = await validator.ValidateAsync(context);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task public_client_without_secret_should_be_able_to_request_token()
    {
        var validator = Factory.CreateClientSecretValidator();

        var context = new DefaultHttpContext();
        var body = "client_id=roclient.public";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var result = await validator.ValidateAsync(context);

        result.IsError.Should().BeFalse();
        result.Client.ClientId.Should().Be("roclient.public");
        result.Client.RequireClientSecret.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task implicit_client_without_secret_should_be_able_to_authenticate()
    {
        var validator = Factory.CreateClientSecretValidator();

        var context = new DefaultHttpContext();
        var body = "client_id=client.implicit";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var result = await validator.ValidateAsync(context);

        result.IsError.Should().BeFalse();
        result.Client.ClientId.Should().Be("client.implicit");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task implicit_client_and_client_creds_without_secret_should_not_be_able_to_authenticate()
    {
        var validator = Factory.CreateClientSecretValidator();

        var context = new DefaultHttpContext();
        var body = "client_id=implicit_and_client_creds";

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var result = await validator.ValidateAsync(context);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task implicit_and_client_credentials_client_with_invalid_secret_should_fail_validation()
    {
        var client = new Client
        {
            ClientId = "implicit_and_client_credentials",
            AllowedGrantTypes = GrantTypes.ImplicitAndClientCredentials,
            RequireClientSecret = true,
            ClientSecrets = { new Secret("secret".Sha256()) }
        };
        var validator = Factory.CreateClientSecretValidator(new InMemoryClientStore(new[] { client }));
        var context = new DefaultHttpContext();
        var body = "client_id=implicit_and_client_credentials&client_secret=invalid";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var result = await validator.ValidateAsync(context);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task implicit_and_client_credentials_client_with_valid_secret_should_pass_validation()
    {
        var client = new Client
        {
            ClientId = "implicit_and_client_credentials",
            AllowedGrantTypes = GrantTypes.ImplicitAndClientCredentials,
            RequireClientSecret = true,
            ClientSecrets = { new Secret("secret".Sha256()) }
        };
        var validator = Factory.CreateClientSecretValidator(new InMemoryClientStore(new[] { client }));
        var context = new DefaultHttpContext();
        var body = "client_id=implicit_and_client_credentials&client_secret=secret";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentType = "application/x-www-form-urlencoded";

        var result = await validator.ValidateAsync(context);

        result.IsError.Should().BeFalse();
        result.Client.ClientId.Should().Be("implicit_and_client_credentials");
    }
}