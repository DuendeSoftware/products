// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using UnitTests.Validation.Setup;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Shouldly;
using UnitTests.Common;
using Xunit;

namespace UnitTests.Validation;

public class UserInfoRequestValidation
{
    private const string Category = "UserInfo Request Validation Tests";
    private IClientStore _clients = new InMemoryClientStore(TestClients.Get());

    [Fact]
    [Trait("Category", Category)]
    public async Task token_without_sub_should_fail()
    {
        var tokenResult = new TokenValidationResult
        {
            IsError = false,
            Client = await _clients.FindEnabledClientByIdAsync("codeclient"),
            Claims = new List<Claim>()
        };

        var validator = new UserInfoRequestValidator(
            new TestTokenValidator(tokenResult),
            new TestProfileService(shouldBeActive: true),
            TestLogger.Create<UserInfoRequestValidator>());

        var result = await validator.ValidateRequestAsync("token");

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task active_user_should_succeed()
    {
        var tokenResult = new TokenValidationResult
        {
            IsError = false,
            Client = await _clients.FindEnabledClientByIdAsync("codeclient"),
            Claims = new List<Claim>
            {
                new Claim("sub", "123")
            },
        };

        var validator = new UserInfoRequestValidator(
            new TestTokenValidator(tokenResult),
            new TestProfileService(shouldBeActive: true),
            TestLogger.Create<UserInfoRequestValidator>());

        var result = await validator.ValidateRequestAsync("token");

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task inactive_user_should_fail()
    {
        var tokenResult = new TokenValidationResult
        {
            IsError = false,
            Client = await _clients.FindEnabledClientByIdAsync("codeclient"),
            Claims = new List<Claim>
            {
                new Claim("sub", "123")
            },
        };

        var validator = new UserInfoRequestValidator(
            new TestTokenValidator(tokenResult),
            new TestProfileService(shouldBeActive: false),
            TestLogger.Create<UserInfoRequestValidator>());

        var result = await validator.ValidateRequestAsync("token");

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(OidcConstants.ProtectedResourceErrors.InvalidToken);
    }
}