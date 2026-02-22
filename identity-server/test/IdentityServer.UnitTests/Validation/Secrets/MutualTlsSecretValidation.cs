// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.Secrets;

public class MutualTlsSecretValidation
{
    private const string Category = "Secrets - MutualTls Secret Validation";

    private IClientStore _clients = new InMemoryClientStore(ClientValidationTestClients.Get());
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    ///////////////////
    // thumbprints
    ///////////////////

    [Fact]
    [Trait("Category", Category)]
    public async Task Thumbprint_invalid_secret_type_should_not_match()
    {
        ISecretValidator validator = new X509ThumbprintSecretValidator(new Logger<X509ThumbprintSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "secret",
            Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret, _ct);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Thumbprint_missing_cert_should_throw()
    {
        ISecretValidator validator = new X509ThumbprintSecretValidator(new Logger<X509ThumbprintSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "secret",
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        Func<Task> act = async () => await validator.ValidateAsync(client.ClientSecrets, secret, _ct);
        await act.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Thumbprint_invalid_secret_should_not_match()
    {
        ISecretValidator validator = new X509ThumbprintSecretValidator(new Logger<X509ThumbprintSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = TestCert.Load(),
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret, _ct);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Thumbprint_valid_secret_should_match()
    {
        ISecretValidator validator = new X509ThumbprintSecretValidator(new Logger<X509ThumbprintSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = TestCert.Load(),
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret, _ct);

        result.Success.ShouldBeTrue();
    }

    ///////////////////
    // names
    ///////////////////

    [Fact]
    [Trait("Category", Category)]
    public async Task Name_invalid_secret_type_should_not_match()
    {
        ISecretValidator validator = new X509NameSecretValidator(new Logger<X509NameSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "secret",
            Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret, _ct);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Name_missing_cert_should_throw()
    {
        ISecretValidator validator = new X509NameSecretValidator(new Logger<X509NameSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "secret",
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        Func<Task> act = async () => await validator.ValidateAsync(client.ClientSecrets, secret, _ct);
        await act.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Name_invalid_secret_should_not_match()
    {
        ISecretValidator validator = new X509NameSecretValidator(new Logger<X509NameSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = TestCert.Load(),
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret, _ct);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Name_valid_secret_should_match()
    {
        ISecretValidator validator = new X509NameSecretValidator(new Logger<X509NameSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = TestCert.Load(),
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret, _ct);

        result.Success.ShouldBeTrue();
    }
}
