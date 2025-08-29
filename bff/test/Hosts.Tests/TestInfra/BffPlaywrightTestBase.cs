// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;
using Projects;
using Xunit.Abstractions;

namespace Hosts.Tests.TestInfra;

[Collection(BffAppHostCollection.CollectionName)]
public class BffPlaywrightTestBase : PlaywrightTestBase<Hosts_AppHost>
{
    public BffPlaywrightTestBase(ITestOutputHelper output, AppHostFixture<Hosts_AppHost> fixture) : base(output, fixture)
    {
    }
}
