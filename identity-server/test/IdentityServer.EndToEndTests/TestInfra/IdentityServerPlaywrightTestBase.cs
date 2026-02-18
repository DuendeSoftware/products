// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;
using Projects;

namespace Duende.IdentityServer.EndToEndTests.TestInfra;

[Collection(IdentityServerAppHostCollection.CollectionName)]
public class IdentityServerPlaywrightTestBase(AppHostFixture<All> fixture)
    : PlaywrightTestBase<All>(fixture);
