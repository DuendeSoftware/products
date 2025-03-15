// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace UnitTests.Validation.Setup;

internal class TestBackchannelAuthenticationRequestIdValidator : IBackchannelAuthenticationRequestIdValidator
{
    private readonly bool shouldError;

    public TestBackchannelAuthenticationRequestIdValidator(bool shouldError = false)
    {
        this.shouldError = shouldError;
    }

    //public DeviceCode DeviceCodeResult { get; set; } = new DeviceCode();

    public Task ValidateAsync(BackchannelAuthenticationRequestIdValidationContext context)
    {
        if (shouldError)
        {
            context.Result = new TokenRequestValidationResult(context.Request, "error");
        }
        else
        {
            context.Result = new TokenRequestValidationResult(context.Request);
        }

        //context.Request.DeviceCode = DeviceCodeResult;

        return Task.CompletedTask;
    }
}
