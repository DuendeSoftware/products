// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Threading.Tasks;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using IdentityModel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.ResponseHandling;

public class PushedAuthorizationResponseGenerator : IPushedAuthorizationResponseGenerator
{
    private readonly IHandleGenerationService _handleGeneration;
    private readonly IDataProtector _dataProtector;
    private readonly IPushedAuthorizationRequestStore _store;
    private readonly IdentityServerOptions _options;
    private readonly ILogger<PushedAuthorizationResponseGenerator> _logger;

    public PushedAuthorizationResponseGenerator(
        IHandleGenerationService handleGeneration,
        IDataProtectionProvider dataProtectionProvider,
        IPushedAuthorizationRequestStore store,
        IdentityServerOptions options,
        ILogger<PushedAuthorizationResponseGenerator> logger
    )
    {
        _handleGeneration = handleGeneration;
        _dataProtector = dataProtectionProvider.CreateProtector("PAR");
        _store = store;
        _options = options;
        _logger = logger;
    }

    public async Task<PushedAuthorizationResponse> CreateResponseAsync(ValidatedPushedAuthorizationRequest request)
    {
        // Create a reference value
        var referenceValue = await _handleGeneration.GenerateAsync(); 
        
        var requestUri = $"urn:ietf:params:oauth:request_uri:{referenceValue}";
        
        // Calculate the expiration
        var expiration = request.Client.PushedAuthorizationLifetime ?? _options.PushedAuthorization.Lifetime;

        // Serialize
        var serialized = ObjectSerializer.ToString(request.Raw.ToFullDictionary());

        // Data Protect
        var protectedData = _dataProtector.Protect(serialized);

        // Persist 
        await _store.StoreAsync(new Models.PushedAuthorizationRequest
        {
            ReferenceValue = referenceValue,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiration),
            Parameters = protectedData
        });

        // TODO - Catch errors and return PushedAuthorizationFailure?

        // Return reference and expiration
        return new PushedAuthorizationSuccess
        {
            RequestUri = requestUri,
            ExpiresIn = expiration
        };
    }
}