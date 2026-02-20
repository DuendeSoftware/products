// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.ResponseHandling;

/// <inheritdoc />
public class PushedAuthorizationResponseGenerator : IPushedAuthorizationResponseGenerator
{
    private readonly IHandleGenerationService _handleGeneration;
    private readonly IdentityServerOptions _options;
    private readonly IPushedAuthorizationService _pushedAuthorizationService;
    private readonly ILogger<PushedAuthorizationResponseGenerator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PushedAuthorizationResponseGenerator"/> class.
    /// </summary>
    /// <param name="handleGeneration">The handle generation service, used for creation of request uri reference values.
    /// </param>
    /// <param name="options">The IdentityServer options</param>
    /// <param name="pushedAuthorizationService">The pushed authorization service</param>
    /// <param name="logger">The logger</param>
    public PushedAuthorizationResponseGenerator(IHandleGenerationService handleGeneration,
        IdentityServerOptions options,
        IPushedAuthorizationService pushedAuthorizationService,
        ILogger<PushedAuthorizationResponseGenerator> logger)
    {
        _handleGeneration = handleGeneration;
        _options = options;
        _pushedAuthorizationService = pushedAuthorizationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PushedAuthorizationResponse> CreateResponseAsync(ValidatedPushedAuthorizationRequest request, CT ct)
    {
        // Create a reference value
        var referenceValue = await _handleGeneration.GenerateAsync(ct);

        var requestUri = $"{IdentityServerConstants.PushedAuthorizationRequestUri}:{referenceValue}";

        // Calculate the expiration
        var expiration = request.Client.PushedAuthorizationLifetime ?? _options.PushedAuthorization.Lifetime;
        var expiresAt = DateTime.UtcNow.AddSeconds(expiration);

        await _pushedAuthorizationService.StoreAsync(new DeserializedPushedAuthorizationRequest
        {
            ReferenceValue = referenceValue,
            ExpiresAtUtc = expiresAt,
            PushedParameters = request.Raw
        }, ct);

        // Return reference and expiration
        return new PushedAuthorizationSuccess
        {
            RequestUri = requestUri,
            ExpiresIn = expiration
        };
    }
}
