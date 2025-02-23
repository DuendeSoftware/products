// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class MockServerUrls : IServerUrls
{
    public string Origin { get; set; }
    public string BasePath { get; set; }
}
