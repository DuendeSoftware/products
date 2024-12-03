// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.v2;

namespace IdentityServer.UnitTests.Licensing.v2;

internal class TestLicenseAccessor : ILicenseAccessor
{
    public License Current { get; set; } = new();
}