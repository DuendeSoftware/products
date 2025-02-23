// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;
using System.Threading.Tasks;

namespace IntegrationTests.Common;

internal class MockCibaUserValidator : IBackchannelAuthenticationUserValidator
{
    public BackchannelAuthenticationUserValidationResult Result { get; set; } = new BackchannelAuthenticationUserValidationResult();
    public BackchannelAuthenticationUserValidatorContext UserValidatorContext { get; set; }

    public Task<BackchannelAuthenticationUserValidationResult> ValidateRequestAsync(BackchannelAuthenticationUserValidatorContext userValidatorContext)
    {
        UserValidatorContext = userValidatorContext;
        return Task.FromResult(Result);
    }
}