// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Interaction.Scenarios.MvcCode;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;

namespace Duende.IdentityServer.Interaction.Tests.Scenarios;

[CollectionDefinition("mvc-code")]
public sealed class MvcCodeCollection : ICollectionFixture<ScenarioFixture<WebClientCodeFlow>>;
