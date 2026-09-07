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
    /// The time provider.
    /// </summary>
    protected readonly TimeProvider TimeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicClientRegistrationRequestProcessor"/> class.
    /// </summary>
    /// <param name="options">The IdentityServer.Configuration options.</param>
    /// <param name="store">The client configuration store.</param>
    /// <param name="timeProvider">The time provider.</param>
    public DynamicClientRegistrationRequestProcessor(
        IdentityServerConfigurationOptions options,
        IClientConfigurationStore store,
        TimeProvider timeProvider)
    {
        Options = options;
        Store = store;
        TimeProvider = timeProvider;
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
    /// <remarks> When a shared secret is generated, this method must set the
    /// "secret" and "plainText" properties of the context's Items
    /// dictionary.</remarks>

    protected virtual async Task<IStepResult> AddClientSecret(
        DynamicClientRegistrationContext context)
    {
        // This method generates a secret, but only when appropriate.
        var authMethod = context.Request.TokenEndpointAuthenticationMethod;

        // We skip generation for the "none" authentication method, where no secret is used by definition.
        if (authMethod is "none")
        {
            return new SuccessfulStep();
        }

        // We also skip for the private_key_jwt authentication method. The client must generate a
        // public-private key pair and share the public key with us.
        if (authMethod is OidcConstants.EndpointAuthenticationMethods.PrivateKeyJwt)
        {
            return new SuccessfulStep();
        }

        // If the client sent us a secret that isn't a JWK, we'll just use that secret.
        // Note that the client might send a JWK secret that it is not using for client
        // authentication. For example, the client might use signed authorization requests
        // (JAR) without private_key_jwt client authentication.
        if (context.Client.ClientSecrets.Any(s => s.Type is not Constants.SecretTypes.Jwk))
        {
            return new SuccessfulStep();
        }

        // Otherwise
        var (secret, plainText) = await GenerateSecret(context);
        context.Items["secret"] = secret;
        context.Items["plainText"] = plainText;
        context.Client.ClientSecrets.Add(secret);
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
            TimeSpan t => TimeProvider.GetUtcNow().UtcDateTime.Add(t)
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
