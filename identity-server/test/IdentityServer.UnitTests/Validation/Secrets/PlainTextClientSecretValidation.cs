// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.Secrets;

public class PlainTextClientSecretValidation
{
    private const string Category = "Secrets - PlainText Shared Secret Validation";

    private ISecretValidator _validator = new PlainTextSharedSecretValidator(new Logger<PlainTextSharedSecretValidator>(new LoggerFactory()));
    private IClientStore _clients = new InMemoryClientStore(ClientValidationTestClients.Get());
    private readonly CT _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Single_Secret()
    {
        var clientId = "single_secret_no_protection_no_expiration";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "secret",
            Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret, _ct);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_Credential_Type()
    {
        var clientId = "single_secret_no_protection_no_expiration";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "secret",
            Type = "invalid"
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret, _ct);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Valid_Multiple_Secrets_No_Protection()
    {
        var clientId = "multiple_secrets_no_protection";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "secret",
            Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret, _ct);
        result.Success.ShouldBeTrue();

        secret.Credential = "foobar";
        result = await _validator.ValidateAsync(client.ClientSecrets, secret, _ct);
        result.Success.ShouldBeTrue();

        secret.Credential = "quux";
        result = await _validator.ValidateAsync(client.ClientSecrets, secret, _ct);
        result.Success.ShouldBeTrue();

        secret.Credential = "notexpired";
        result = await _validator.ValidateAsync(client.ClientSecrets, secret, _ct);
        result.Success.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_Single_Secret()
    {
        var clientId = "single_secret_no_protection_no_expiration";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "invalid",
            Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret, _ct);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_Multiple_Secrets()
    {
        var clientId = "multiple_secrets_no_protection";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "invalid",
            Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret, _ct);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Client_with_no_Secret_Should_Fail()
    {
        var clientId = "no_secret_client";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret, _ct);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Client_with_null_Secret_Should_Fail()
    {
        var clientId = "null_secret_client";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret,
            Credential = "secret"
        };

        var result = await _validator.ValidateAsync(client.ClientSecrets, secret, _ct);
        result.Success.ShouldBeFalse();
    }
}
