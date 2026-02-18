// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;
using Projects;

namespace Hosts.Tests.TestInfra;

[Collection(BffAppHostCollection.CollectionName)]
public class BffPlaywrightTestBase : PlaywrightTestBase<Hosts_AppHost>
{
    public BffPlaywrightTestBase(AppHostFixture<Hosts_AppHost> fixture) : base(fixture)
    {
    }
}
