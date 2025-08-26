// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.Configuration;
using Duende.IdentityServer.Configuration.Models;
using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;
using Duende.IdentityServer.Configuration.RequestProcessing;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace IdentityServerHost.Extensions;

[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via DI container")]
public sealed class CustomClientRegistrationProcessor(
    IdentityServerConfigurationOptions options,
    IClientConfigurationStore dcrStore,
    IClientStore clientStore) : DynamicClientRegistrationRequestProcessor(options, dcrStore)
{

    protected override async Task<IStepResult> AddClientId(DynamicClientRegistrationContext context)
    {
        if (context.Request.Extensions.TryGetValue("client_id", out var clientIdParameter))
        {
            var clientId = clientIdParameter.ToString();
            if (clientId != null)
            {
                var existingClient = clientStore.FindClientByIdAsync(clientId);
                if (existingClient is not null)
                {
                    return new DynamicClientRegistrationError(
                        "Duplicate client id",
                        "Attempt to register a client with a client id that has already been registered"
                    );
                }
                else
                {
                    context.Client.ClientId = clientId;
                    return new SuccessfulStep();
                }
            }
        }
        return await base.AddClientId(context);
    }

    protected override async Task<(Secret, string)> GenerateSecret(DynamicClientRegistrationContext context)
    {
        if (context.Request.Extensions.TryGetValue("client_secret", out var secretParam))
        {
            var plainText = secretParam.ToString();
            ArgumentNullException.ThrowIfNull(plainText);
            var secret = new Secret(plainText.Sha256());

            return (secret, plainText);
        }
        return await base.GenerateSecret(context);

    }
}
