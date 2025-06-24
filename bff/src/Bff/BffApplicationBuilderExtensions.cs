// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Builder;
using Duende.Bff.Endpoints;
using Duende.Bff.SessionManagement.SessionStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Bff;

public static class BffApplicationBuilderExtensions
{
    public static IBffEndpointBuilder EnableBffEndpoint(this IBffApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.TryGetPart<IBffEndpointBuilder>(out _))
        {
            throw new InvalidOperationException("BffEndpoint is already enabled. You can only enable it once per application.");
        }
        var builder = new BffEndpointBuilder(app);

        if (app.TryGetPart<IBffSessionBuilder>(out _))
        {
            // session's have already been added. Make sure it's configured on the BFF endpoint as well
            AddServerSideSessionsToEndpoint(builder);
        }

        app.Parts.Add(builder);
        return builder;
    }

    public static IBffSessionBuilder EnableServerSideSessions(this IBffApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        // Add server side sessions to endpoint
        var sessionBuilder = new BffSessionBuilder(app);

        if (app.TryGetPart<IBffSessionBuilder>(out _))
        {
            throw new InvalidOperationException("Server side sessions are already enabled. You can only enable them once per application.");
        }

        if (app.TryGetPart<IBffEndpointBuilder>(out var endpointBuilder))
        {
            // The endpointbuilder is already added. Make sure the server side session logic is
            // configured on it. 
            AddServerSideSessionsToEndpoint(endpointBuilder);
        }

        app.HostBuilder.Services.TryAddSingleton<IUserSessionStore, InMemoryUserSessionStore>();
        app.Parts.Add(sessionBuilder);
        return sessionBuilder;
    }

    private static void AddServerSideSessionsToEndpoint(IBffEndpointBuilder endpointBuilder)
    {
        endpointBuilder.Services.AddServerSideSessionsSupportingServices();
        endpointBuilder.Services.AddDelegatedToRootContainer<IUserSessionStore>();
    }

    public static IBffEndpointBuilder UsingCustomEndpoint<TEndpoint, TImplementation>(this IBffEndpointBuilder builder)
        where TEndpoint : class, IBffEndpoint
        where TImplementation : class, TEndpoint
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddTransient<TEndpoint, TImplementation>();
        return builder;
    }

    public static void EnableManagementApi(this IBffApplicationBuilder app)
    {

    }

    public static void EnableManagementUI(this IBffApplicationBuilder app)
    {
    }

}
