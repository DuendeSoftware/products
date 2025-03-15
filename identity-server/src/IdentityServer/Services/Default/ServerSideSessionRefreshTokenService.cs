// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Configuration.DependencyInjection;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Decorator on the refresh token service to coordinate refresh token lifetimes and server-side sessions.
/// </summary>
internal class ServerSideSessionRefreshTokenService : IRefreshTokenService
{
    /// <summary>
    /// The inner IRefreshTokenService implementation.
    /// </summary>
    protected readonly IRefreshTokenService Inner;

    /// <summary>
    /// The session coordination service.
    /// </summary>
    protected readonly ISessionCoordinationService SessionCoordinationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRefreshTokenService" /> class.
    /// </summary>
    public ServerSideSessionRefreshTokenService(
        Decorator<IRefreshTokenService> inner,
        ISessionCoordinationService sessionCoordinationService)
    {
        Inner = inner.Instance;
        SessionCoordinationService = sessionCoordinationService;
    }

    private static readonly TokenValidationResult TokenValidationError = new TokenValidationResult
    {
        IsError = true,
        Error = OidcConstants.TokenErrors.InvalidGrant
    };


    /// <inheritdoc/>
    public virtual async Task<TokenValidationResult> ValidateRefreshTokenAsync(string tokenHandle, Client client)
    {
        var result = await Inner.ValidateRefreshTokenAsync(tokenHandle, client);

        using var activity = Tracing.ServiceActivitySource.StartActivity("ServerSideSessionRefreshTokenService.ValidateRefreshToken");

        if (!result.IsError)
        {
            var valid = await SessionCoordinationService.ValidateSessionAsync(new SessionValidationRequest
            {
                SubjectId = result.RefreshToken.SubjectId,
                SessionId = result.RefreshToken.SessionId,
                Client = result.Client,
                Type = SessionValidationType.RefreshToken
            });

            if (!valid)
            {
                result = TokenValidationError;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public Task<string> CreateRefreshTokenAsync(RefreshTokenCreationRequest request)
    {
        return Inner.CreateRefreshTokenAsync(request);
    }

    /// <inheritdoc/>
    public Task<string> UpdateRefreshTokenAsync(RefreshTokenUpdateRequest request)
    {
        return Inner.UpdateRefreshTokenAsync(request);
    }
}
