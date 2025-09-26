// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;
using Xunit.Abstractions;

namespace Hosts.Tests.TestInfra;

[Collection(BffAppHostCollection.CollectionName)]
public class BffPlaywrightTestBase : PlaywrightTestBase
{
    public BffPlaywrightTestBase(ITestOutputHelper output, AppHostFixture fixture) : base(output, fixture)
    {
    }
}
