// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace UnitTests.Validation.Setup;

public static class ValidationExtensions
{
    public static ClientSecretValidationResult ToValidationResult(this Client client, ParsedSecret secret = null) => new ClientSecretValidationResult
    {
        Client = client,
        Secret = secret
    };
}
