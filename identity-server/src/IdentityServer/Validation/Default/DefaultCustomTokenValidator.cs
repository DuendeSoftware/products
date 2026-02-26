// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default custom token validator
/// </summary>
public class DefaultCustomTokenValidator : ICustomTokenValidator
{
    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The user service
    /// </summary>
    protected readonly IProfileService Profile;

    /// <summary>
    /// The client store
    /// </summary>
    protected readonly IClientStore Clients;

    /// <inheritdoc/>
    public virtual Task<TokenValidationResult> ValidateAccessTokenAsync(TokenValidationResult result, Ct _) => Task.FromResult(result);

    /// <inheritdoc/>
    public virtual Task<TokenValidationResult> ValidateIdentityTokenAsync(TokenValidationResult result, Ct _) => Task.FromResult(result);
}
