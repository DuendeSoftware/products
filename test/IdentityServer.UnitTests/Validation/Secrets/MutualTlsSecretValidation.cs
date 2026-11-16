// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using FluentAssertions;
using UnitTests.Common;
using UnitTests.Validation.Setup;
using Microsoft.Extensions.Logging;
using Xunit;

namespace UnitTests.Validation.Secrets;

public class MutualTlsSecretValidation
{
    private const string Category = "Secrets - MutualTls Secret Validation";
       
    private IClientStore _clients = new InMemoryClientStore(ClientValidationTestClients.Get());

    ///////////////////
    // thumbprints
    ///////////////////

    [Fact]
    [Trait("Category", Category)]
    public async Task Thumbprint_invalid_secret_type_should_not_match()
    {
        ISecretValidator validator = new X509ThumbprintSecretValidator(new Logger<X509ThumbprintSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "secret",
            Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Thumbprint_missing_cert_should_throw()
    {
        ISecretValidator validator = new X509ThumbprintSecretValidator(new Logger<X509ThumbprintSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "secret",
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        Func<Task> act = async () => await validator.ValidateAsync(client.ClientSecrets, secret);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Thumbprint_invalid_secret_should_not_match()
    {
        ISecretValidator validator = new X509ThumbprintSecretValidator(new Logger<X509ThumbprintSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = TestCert.Load(),
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Thumbprint_valid_secret_should_match()
    {
        ISecretValidator validator = new X509ThumbprintSecretValidator(new Logger<X509ThumbprintSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = TestCert.Load(),
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.Should().BeTrue();
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
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "secret",
            Type = IdentityServerConstants.ParsedSecretTypes.SharedSecret
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Name_missing_cert_should_throw()
    {
        ISecretValidator validator = new X509NameSecretValidator(new Logger<X509NameSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = "secret",
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        Func<Task> act = async () => await validator.ValidateAsync(client.ClientSecrets, secret);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Name_invalid_secret_should_not_match()
    {
        ISecretValidator validator = new X509NameSecretValidator(new Logger<X509NameSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_invalid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        using var clientCertificate = CreateCaIssuedCertificate("CN=identityserver_testing");

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = clientCertificate,
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Name_valid_secret_should_match()
    {
        ISecretValidator validator = new X509NameSecretValidator(new Logger<X509NameSecretValidator>(new LoggerFactory()));

        var clientId = "mtls_client_valid";
        var client = await _clients.FindEnabledClientByIdAsync(clientId);

        using var clientCertificate = CreateCaIssuedCertificate("CN=identityserver_testing");

        var secret = new ParsedSecret
        {
            Id = clientId,
            Credential = clientCertificate,
            Type = IdentityServerConstants.ParsedSecretTypes.X509Certificate
        };

        var result = await validator.ValidateAsync(client.ClientSecrets, secret);

        result.Success.Should().BeTrue();
    }

    private static X509Certificate2 CreateCaIssuedCertificate(string subject)
    {
        using var caKey = RSA.Create(2048);
        var caRequest = new CertificateRequest("CN=Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var ca = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        using var clientKey = RSA.Create(2048);
        var clientRequest = new CertificateRequest(subject, clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return clientRequest.Create(ca, ca.NotBefore, ca.NotAfter, RandomNumberGenerator.GetBytes(8));
    }
}
