// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;
using Hosts.ServiceDefaults;
using Projects;

namespace Hosts.Tests.TestInfra;

public class BffHostTestFixture() : AppHostFixture<Hosts_AppHost>(new BffAppHostRoutes())
{
}
