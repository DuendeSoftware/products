// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;
using Projects;
using ServiceDefaults;

namespace Duende.IdentityServer.EndToEndTests.TestInfra;

public class IdentityServerHostTestFixture() : AppHostFixture<Dev>(new IdentityServerAppHostRoutes())
{
}
