// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Configuration.Configuration;
using Duende.IdentityServer.Configuration.Models;
using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Configuration.RequestProcessing;

/// <inheritdoc />
public class DynamicClientRegistrationRequestProcessor : IDynamicClientRegistrationRequestProcessor
{
    /// <summary>
    /// The options.
    /// </summary>
    protected readonly IdentityServerConfigurationOptions Options;

    /// <summary>
    /// The client configuration store.
    /// </summary>
    protected readonly IClientConfigurationStore Store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicClientRegistrationRequestProcessor"/> class.
    /// </summary>
    /// <param name="options">The IdentityServer.Configuration options.</param>
    /// <param name="store">The client configuration store.</param>
    public DynamicClientRegistrationRequestProcessor(
        IdentityServerConfigurationOptions options,
        IClientConfigurationStore store)
    {
        Options = options;
        Store = store;
    }


    /// <inheritdoc />
    public virtual async Task<IDynamicClientRegistrationResponse> ProcessAsync(
        DynamicClientRegistrationContext context, Ct ct)
    {
        var clientIdResult = await AddClientId(context, ct);
        if (clientIdResult is DynamicClientRegistrationError clientIdFailure)
        {
            return clientIdFailure;
        }

        Secret? secret = null;
        string? plainText = null;
        var clientSecretResult = await AddClientSecret(context);
        if (clientSecretResult is DynamicClientRegistrationError clientSecretFailure)
        {
            return clientSecretFailure;
        }
        else if (clientSecretResult is SuccessfulStep)
        {
            if (context.Items.TryGetValue("secret", out var secretValue) && secretValue is Secret s &&
               context.Items.TryGetValue("plainText", out var plainTextValue) && plainTextValue is string pt)
            {
                secret = s;
                plainText = pt;
            }
        }

        await Store.AddAsync(context.Client, ct);

        return new DynamicClientRegistrationResponse(context.Request, context.Client)
        {
            ClientId = context.Client.ClientId,
            ClientSecret = plainText,
            ClientSecretExpiresAt = secret switch
            {
                null => null,
                { Expiration: null } => 0,
                { Expiration: DateTime e } => new DateTimeOffset(e).ToUnixTimeSeconds()
            }
        };
    }

    /// <summary>
    /// Adds a client secret to a dynamic client registration request.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model, the DCR request, and other contextual
    /// information.</param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    /// <remark> This method must set the "secret" and "plainText" properties of
    /// the context's Items dictionary.</remark>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>

    protected virtual async Task<IStepResult> AddClientSecret(
        DynamicClientRegistrationContext context)
    {
        if (context.Client.ClientSecrets.Count == 0 && context.Request.TokenEndpointAuthenticationMethod != "none")
        {
            var (secret, plainText) = await GenerateSecret(context);
            context.Items["secret"] = secret;
            context.Items["plainText"] = plainText;
            context.Client.ClientSecrets.Add(secret);
        }
        return new SuccessfulStep();
    }

    /// <summary>
    /// Generates a secret for a dynamic client registration request.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model, the DCR request, and other contextual
    /// information.</param>
    /// <returns>A task that returns a tuple containing the generated secret and
    /// the plaintext of that secret.</returns>
    protected virtual Task<(Secret secret, string plainText)> GenerateSecret(
        DynamicClientRegistrationContext context)
    {
        var plainText = CryptoRandom.CreateUniqueId();
        DateTime? lifetime = Options.DynamicClientRegistration.SecretLifetime switch
        {
            null => null,
            TimeSpan t => DateTime.UtcNow.Add(t)
        };
        var secret = new Secret(plainText.ToSha256(), lifetime);
        return Task.FromResult((secret, plainText));
    }

    /// <summary>
    /// Generates a client ID and adds it to the validatedRequest's client
    /// model.
    /// </summary>
    /// <param name="context">The dynamic client registration context, which
    /// includes the client model, the DCR request, and other contextual
    /// information.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    protected virtual Task<IStepResult> AddClientId(
        DynamicClientRegistrationContext context, Ct ct)
    {
        context.Client.ClientId = CryptoRandom.CreateUniqueId();
        return StepResult.Success();
    }
}
