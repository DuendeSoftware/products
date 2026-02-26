// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Buffers.Text;
using System.Collections.Specialized;
using System.Text;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.TokenRequest_Validation;

public class TokenRequestValidation_PKCE
{
    private const string Category = "TokenRequest Validation - PKCE";

    private IClientStore _clients = Factory.CreateClientStore();
    private InputLengthRestrictions lengths = new InputLengthRestrictions();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("codeclient.pkce")]
    [InlineData("codeclient")]
    [Trait("Category", Category)]
    public async Task valid_pkce_token_request_with_plain_method_should_succeed(string clientId)
    {
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);
        var grants = Factory.CreateAuthorizationCodeStore();
        var verifier = "x".Repeat(lengths.CodeVerifierMinLength);

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("bob").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",
            CodeChallenge = verifier.Sha256(),
            CodeChallengeMethod = OidcConstants.CodeChallengeMethods.Plain,

            RequestedScopes = new List<string>
            {
                "openid"
            }
        };

        var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.CodeVerifier, verifier);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_pkce_token_request_with_plain_method_should_succeed_hybrid()
    {
        var client = await _clients.FindEnabledClientByIdAsync("hybridclient.pkce", _ct);
        var grants = Factory.CreateAuthorizationCodeStore();
        var verifier = "x".Repeat(lengths.CodeVerifierMinLength);

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",
            CodeChallenge = verifier.Sha256(),
            CodeChallengeMethod = OidcConstants.CodeChallengeMethods.Plain,

            RequestedScopes = new List<string>
            {
                "openid"
            }
        };

        var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.CodeVerifier, verifier);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);

        result.IsError.ShouldBeFalse();
    }

    [Theory]
    [InlineData("codeclient.pkce")]
    [InlineData("codeclient")]
    [Trait("Category", Category)]
    public async Task valid_pkce_token_request_with_sha256_method_should_succeed(string clientId)
    {
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);
        var grants = Factory.CreateAuthorizationCodeStore();

        var verifier = "x".Repeat(lengths.CodeVerifierMinLength);
        var challenge = VerifierToSha256CodeChallenge(verifier);

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",
            CodeChallenge = challenge.Sha256(),
            CodeChallengeMethod = OidcConstants.CodeChallengeMethods.Sha256,

            RequestedScopes = new List<string>
            {
                "openid"
            }
        };

        var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.CodeVerifier, verifier);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);

        result.IsError.ShouldBeFalse();
    }

    [Theory]
    [InlineData("codeclient.pkce")]
    [Trait("Category", Category)]
    public async Task token_request_with_missing_code_challenge_and_verifier_should_fail(string clientId)
    {
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);
        var grants = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",
            RequestedScopes = new List<string>
            {
                "openid"
            }
        };

        var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Theory]
    [InlineData("codeclient.pkce")]
    [InlineData("codeclient")]
    [Trait("Category", Category)]
    public async Task token_request_with_missing_code_challenge_should_fail(string clientId)
    {
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);
        var grants = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",

            RequestedScopes = new List<string>
            {
                "openid"
            }
        };

        var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.CodeVerifier, "x".Repeat(lengths.CodeVerifierMinLength));
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Theory]
    [InlineData("codeclient.pkce")]
    [InlineData("codeclient")]
    [Trait("Category", Category)]
    public async Task token_request_with_invalid_verifier_plain_method_should_fail(string clientId)
    {
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);
        var grants = Factory.CreateAuthorizationCodeStore();
        var verifier = "x".Repeat(lengths.CodeVerifierMinLength);

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",
            CodeChallenge = verifier.Sha256(),
            CodeChallengeMethod = OidcConstants.CodeChallengeMethods.Plain,

            RequestedScopes = new List<string>
            {
                "openid"
            }
        };

        var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.CodeVerifier, verifier + "invalid");
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Theory]
    [InlineData("codeclient.pkce")]
    [InlineData("codeclient")]
    [Trait("Category", Category)]
    public async Task token_request_with_invalid_verifier_sha256_method_should_fail(string clientId)
    {
        var client = await _clients.FindEnabledClientByIdAsync(clientId, _ct);
        var grants = Factory.CreateAuthorizationCodeStore();

        var verifier = "x".Repeat(lengths.CodeVerifierMinLength);
        var challenge = VerifierToSha256CodeChallenge(verifier);

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",
            CodeChallenge = challenge.Sha256(),
            CodeChallengeMethod = OidcConstants.CodeChallengeMethods.Sha256,

            RequestedScopes = new List<string>
            {
                "openid"
            }
        };

        var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.CodeVerifier, verifier + "invalid");
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), _ct);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    private static string VerifierToSha256CodeChallenge(string codeVerifier)
    {
        var codeVerifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var hashedBytes = codeVerifierBytes.Sha256();
        var transformedCodeVerifier = Base64Url.EncodeToString(hashedBytes);

        return transformedCodeVerifier;
    }
}
