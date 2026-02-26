// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.IntegrationTests.Common;

internal class MockCustomBackchannelAuthenticationValidator : ICustomBackchannelAuthenticationValidator
{
    public CustomBackchannelAuthenticationRequestValidationContext Context { get; set; }


    /// <summary>
    /// An action that will be performed by the mock custom validator.
    /// </summary>
    public Action<CustomBackchannelAuthenticationRequestValidationContext> Thunk { get; set; } = delegate { };

    public Task ValidateAsync(CustomBackchannelAuthenticationRequestValidationContext customValidationContext, Ct _)
    {
        Thunk(customValidationContext);
        Context = customValidationContext;
        return Task.CompletedTask;
    }
}
