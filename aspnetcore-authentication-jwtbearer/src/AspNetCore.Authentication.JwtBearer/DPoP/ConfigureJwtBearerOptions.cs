// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Ensures that the <see cref="JwtBearerOptions"/> are configured with <see cref="DPoPJwtBearerEvents"/>.
/// </summary>
internal sealed class ConfigureJwtBearerOptions(DPoPJwtBearerEvents dpopEvents) : IPostConfigureOptions<JwtBearerOptions>
{
    public string? Scheme { get; set; }
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        if (Scheme != name)
        {
            return;
        }
        options.Events ??= new JwtBearerEvents(); // Despite nullability annotations saying this is unnecessary, it sometimes is null
        options.Events.OnChallenge = CreateChallengeCallback(options.Events.OnChallenge, dpopEvents);
        options.Events.OnMessageReceived = CreateMessageReceivedCallback(options.Events.OnMessageReceived, dpopEvents);
        options.Events.OnTokenValidated = CreateTokenValidatedCallback(options.Events.OnTokenValidated, dpopEvents);
    }

    private Func<JwtBearerChallengeContext, Task> CreateChallengeCallback(Func<JwtBearerChallengeContext, Task> inner, DPoPJwtBearerEvents dpopEvents)
    {
        async Task Callback(JwtBearerChallengeContext ctx)
        {
            await inner(ctx);
            await dpopEvents.Challenge(ctx);
        }
        return Callback;
    }

    private Func<MessageReceivedContext, Task> CreateMessageReceivedCallback(Func<MessageReceivedContext, Task> inner, DPoPJwtBearerEvents dpopEvents)
    {
        async Task Callback(MessageReceivedContext ctx)
        {
            await inner(ctx);
            await dpopEvents.MessageReceived(ctx);
        }
        return Callback;
    }

    private Func<TokenValidatedContext, Task> CreateTokenValidatedCallback(Func<TokenValidatedContext, Task> inner, DPoPJwtBearerEvents dpopEvents)
    {
        async Task Callback(TokenValidatedContext ctx)
        {
            await inner(ctx);
            await dpopEvents.TokenValidated(ctx);
        }
        return Callback;
    }
}
