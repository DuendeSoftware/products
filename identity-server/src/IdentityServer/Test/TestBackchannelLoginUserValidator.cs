// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Test;
using Duende.IdentityServer.Validation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Implementation of IBackchannelAuthenticationUserValidator using the test user store.
/// </summary>
public class TestBackchannelLoginUserValidator : IBackchannelAuthenticationUserValidator
{
    private readonly TestUserStore _testUserStore;

    /// <summary>
    /// Ctor
    /// </summary>
    public TestBackchannelLoginUserValidator(TestUserStore testUserStore) => _testUserStore = testUserStore;

    /// <inheritdoc/>
    public Task<BackchannelAuthenticationUserValidationResult> ValidateRequestAsync(BackchannelAuthenticationUserValidatorContext userValidatorContext)
    {
        var result = new BackchannelAuthenticationUserValidationResult();

        TestUser user = default;

        if (userValidatorContext.LoginHint != null)
        {
            user = _testUserStore.FindByUsername(userValidatorContext.LoginHint);
        }
        else if (userValidatorContext.IdTokenHintClaims != null)
        {
            user = _testUserStore.FindBySubjectId(userValidatorContext.IdTokenHintClaims.SingleOrDefault(x => x.Type == JwtClaimTypes.Subject)?.Value);
        }

        if (user != null && user.IsActive)
        {
            var claims = new List<Claim>
            {
                new(JwtClaimTypes.Subject, user.SubjectId)
            };
            var ci = new ClaimsIdentity(claims, "ciba");
            result.Subject = new ClaimsPrincipal(ci);
        }

        return Task.FromResult(result);
    }
}
