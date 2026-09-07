// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.Scenarios.Ciba;
using Duende.IdentityServer.Interaction.Scenarios.ConsoleFlows;
using Duende.IdentityServer.Interaction.Scenarios.DPoP;
using Duende.IdentityServer.Interaction.Scenarios.MvcCode;
using Duende.IdentityServer.Interaction.Scenarios.MvcSaml;
using Duende.IdentityServer.Interaction.Scenarios.SamlSp;
using Duende.IdentityServer.Interaction.Scenarios.Spaces;
using Duende.IdentityServer.Interaction.Scenarios.TokenManagement;

var builder = DistributedApplication.CreateBuilder(args);

// Register scenarios. Each one appears as a resource in the Aspire dashboard
// with Start/Stop commands. No auto-start; use the dashboard to launch them.
builder.AddScenario(new WebClientCodeFlow());
builder.AddScenario(new ConsoleClientCredentials());
builder.AddScenario(new ConsoleClientCredentialsCallingIdentityServerApi());
builder.AddScenario(new ConsoleClientCredentialsPostBody());
builder.AddScenario(new AutomaticTokenManagement());
builder.AddScenario(new CibaFlow());
builder.AddScenario(new MvcDPoPFlow());
builder.AddScenario(new ClientCredentialsDPoP());
builder.AddScenario(new DeviceFlow());
builder.AddScenario(new ConsoleResourceOwnerFlowRefreshToken());
builder.AddScenario(new ResourceOwnerFlow());
builder.AddScenario(new ResourceOwnerFlowPublic());
builder.AddScenario(new ResourceOwnerFlowReference());
builder.AddScenario(new ResourceOwnerFlowUserInfo());
builder.AddScenario(new ParameterizedScope());
builder.AddScenario(new CustomGrant());
builder.AddScenario(new PrivateKeyJwt());
builder.AddScenario(new TokenIntrospection());
builder.AddScenario(new HybridBackChannel());
builder.AddScenario(new JarJwt());
builder.AddScenario(new JarUriJwt());
builder.AddScenario(new MvcSamlFlow());
builder.AddScenario(new SamlSpPostBindingFlow());
builder.AddScenario(new WebSecurityBaseline());
builder.AddScenario(new Spaces());

builder.Build().Run();
