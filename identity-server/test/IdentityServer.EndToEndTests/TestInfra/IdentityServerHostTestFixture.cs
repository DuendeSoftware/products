// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;
using ServiceDefaults;

namespace Duende.IdentityServer.EndToEndTests.TestInfra;

public class IdentityServerHostTestFixture() : AppHostFixture(new IdentityServerAppHostRoutes())
{
}
