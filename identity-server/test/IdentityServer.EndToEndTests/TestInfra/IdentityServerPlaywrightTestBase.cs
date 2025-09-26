// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;
using Xunit.Abstractions;

namespace Duende.IdentityServer.EndToEndTests.TestInfra;

[Collection(IdentityServerAppHostCollection.CollectionName)]
public class IdentityServerPlaywrightTestBase(ITestOutputHelper output, AppHostFixture fixture)
    : PlaywrightTestBase(output, fixture);
