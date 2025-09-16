// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;
using Projects;
using Xunit.Abstractions;

namespace Duende.IdentityServer.EndToEndTests.TestInfra;

[Collection(IdentityServerAppHostCollection.CollectionName)]
public class IdentityServerPlaywrightTestBase(ITestOutputHelper output, AppHostFixture<Dev> fixture)
    : PlaywrightTestBase<Dev>(output, fixture);
