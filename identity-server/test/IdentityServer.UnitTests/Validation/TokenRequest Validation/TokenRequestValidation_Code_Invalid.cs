// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation.TokenRequest_Validation;

public class TokenRequestValidation_Code_Invalid
{
    private IClientStore _clients = Factory.CreateClientStore();
    private const string Category = "TokenRequest Validation - AuthorizationCode - Invalid";
    private readonly CT _ct = TestContext.Current.CancellationToken;

    private ClaimsPrincipal _subject = new IdentityServerUser("bob").CreatePrincipal();

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_AuthorizationCode()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var store = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            IsOpenId = true,
            RedirectUri = "https://server/cb",
            Subject = _subject
        };

        var handle = await store.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_AuthorizationCode()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var store = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            IsOpenId = true,
            RedirectUri = "https://server/cb",
            Subject = _subject
        };

        var handle = await store.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, "invalid");
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task AuthorizationCodeTooLong()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var store = Factory.CreateAuthorizationCodeStore();
        var options = new IdentityServerOptions();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            IsOpenId = true,
            RedirectUri = "https://server/cb",
            Subject = _subject
        };

        var handle = await store.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store);
        var longCode = "x".Repeat(options.InputLengthRestrictions.AuthorizationCode + 1);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, longCode);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task No_Scopes_for_AuthorizationCode()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var store = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            IsOpenId = true,
            RedirectUri = "https://server/cb",
            Subject = _subject
        };

        var handle = await store.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        OidcConstants.TokenErrors.InvalidRequest.ShouldBe(result.Error);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Client_Not_Authorized_For_AuthorizationCode_Flow()
    {
        var client = await _clients.FindEnabledClientByIdAsync("implicitclient", _ct);
        var store = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            IsOpenId = true,
            RedirectUri = "https://server/cb",
            Subject = _subject
        };

        var handle = await store.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.UnauthorizedClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Client_Trying_To_Request_Token_Using_Another_Clients_Code()
    {
        var client1 = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var client2 = await _clients.FindEnabledClientByIdAsync("codeclient_restricted", _ct);
        var store = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            ClientId = client1.ClientId,
            Lifetime = client1.AuthorizationCodeLifetime,
            IsOpenId = true,
            RedirectUri = "https://server/cb",
            Subject = _subject
        };

        var handle = await store.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client2.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_RedirectUri()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var store = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            IsOpenId = true,
            RedirectUri = "https://server/cb",
            Subject = _subject
        };

        var handle = await store.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.UnauthorizedClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Different_RedirectUri_Between_Authorize_And_Token_Request()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var store = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            IsOpenId = true,
            RedirectUri = "https://server1/cb",
            Subject = _subject
        };

        var handle = await store.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server2/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Expired_AuthorizationCode()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var store = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            IsOpenId = true,
            RedirectUri = "https://server/cb",
            CreationTime = DateTime.UtcNow.AddSeconds(-100),
            Subject = _subject
        };

        var handle = await store.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Reused_AuthorizationCode()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var store = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            IsOpenId = true,
            RedirectUri = "https://server/cb",
            RequestedScopes = new List<string>
            {
                "openid"
            }
        };

        var handle = await store.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        // request first time
        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeFalse();

        // request second time
        validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store);

        result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Code_Request_with_disabled_User()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var store = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            RedirectUri = "https://server/cb",
            RequestedScopes = new List<string>
            {
                "openid"
            }
        };

        var handle = await store.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: store,
            profile: new TestProfileService(shouldBeActive: false));

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidGrant);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_resource_indicator()
    {
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
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
                "openid", "scope1"
            },
            RequestedResourceIndicators = new[] { "urn:api1", "urn:api2" }
        };

        var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);

        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants);

        var parameters = new NameValueCollection();
        parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
        parameters.Add(OidcConstants.TokenRequest.Code, handle);
        parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");
        parameters.Add(OidcConstants.TokenRequest.Resource, "urn:api1" + new string('x', 512));

        {
            var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_target");
        }
        {
            parameters[OidcConstants.TokenRequest.Resource] = "api";

            var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());
            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_target");
        }
        {
            parameters[OidcConstants.TokenRequest.Resource] = "urn:api3";

            var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());
            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_target");
        }
        {
            parameters[OidcConstants.TokenRequest.Resource] = "urn:api1";
            parameters.Add(OidcConstants.TokenRequest.Resource, "urn:api2");

            var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());
            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_target");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task failed_resource_validation_should_fail()
    {
        var mockResourceValidator = new MockResourceValidator();
        var client = await _clients.FindEnabledClientByIdAsync("codeclient", _ct);
        var grants = Factory.CreateAuthorizationCodeStore();

        {
            var code = new AuthorizationCode
            {
                CreationTime = DateTime.UtcNow,
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                ClientId = client.ClientId,
                Lifetime = client.AuthorizationCodeLifetime,
                RedirectUri = "https://server/cb",
                RequestedScopes = new List<string>
                {
                    "openid", "scope1"
                },
                RequestedResourceIndicators = new[] { "urn:api1", "urn:api2" }
            };

            var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);
            var validator = Factory.CreateTokenRequestValidator(resourceValidator: mockResourceValidator, authorizationCodeStore: grants);

            var parameters = new NameValueCollection();
            parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
            parameters.Add(OidcConstants.TokenRequest.Code, handle);
            parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");
            parameters.Add(OidcConstants.TokenRequest.Resource, "urn:api1");

            mockResourceValidator.Result = new ResourceValidationResult
            {
                InvalidScopes = { "foo" }
            };
            var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_scope");
        }

        {
            var code = new AuthorizationCode
            {
                CreationTime = DateTime.UtcNow,
                Subject = new IdentityServerUser("123").CreatePrincipal(),
                ClientId = client.ClientId,
                Lifetime = client.AuthorizationCodeLifetime,
                RedirectUri = "https://server/cb",
                RequestedScopes = new List<string>
                {
                    "openid", "scope1"
                },
                RequestedResourceIndicators = new[] { "urn:api1", "urn:api2" }
            };

            var handle = await grants.StoreAuthorizationCodeAsync(code, _ct);
            var validator = Factory.CreateTokenRequestValidator(resourceValidator: mockResourceValidator, authorizationCodeStore: grants);

            var parameters = new NameValueCollection();
            parameters.Add(OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode);
            parameters.Add(OidcConstants.TokenRequest.Code, handle);
            parameters.Add(OidcConstants.TokenRequest.RedirectUri, "https://server/cb");
            parameters.Add(OidcConstants.TokenRequest.Resource, "urn:api1");

            mockResourceValidator.Result = new ResourceValidationResult
            {
                InvalidResourceIndicators = { "foo" }
            };
            var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult());

            result.IsError.ShouldBeTrue();
            result.Error.ShouldBe("invalid_target");
        }
    }
}
