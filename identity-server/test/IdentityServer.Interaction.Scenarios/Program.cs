// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.Scenarios.Ciba;
using Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;
using Duende.IdentityServer.Interaction.Scenarios.DPoP;
using Duende.IdentityServer.Interaction.Scenarios.MvcCode;
using Duende.IdentityServer.Interaction.Scenarios.TokenManagement;

var builder = DistributedApplication.CreateBuilder(args);

// Register scenarios — each one appears as a resource in the Aspire dashboard
// with Start/Stop commands. No auto-start; use the dashboard to launch them.
builder.AddScenario(new WebClientCodeFlow());
builder.AddScenario(new ConsoleClientCredentials());
builder.AddScenario(new AutomaticTokenManagement());
builder.AddScenario(new CibaFlow());
builder.AddScenario(new MvcDPoPFlow());
builder.AddScenario(new ClientCredentialsDPoP());
builder.AddScenario(new DeviceFlow());
builder.AddScenario(new ResourceOwnerFlow());
builder.AddScenario(new PrivateKeyJwt());
builder.AddScenario(new TokenIntrospection());
builder.AddScenario(new HybridBackChannel());
builder.AddScenario(new JarJwt());

builder.Build().Run();
